using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Riftcaster.Admin;
using Riftcaster.Contracts;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;
using Riftcaster.Core.LowerThird;
using Riftcaster.Core.Match;
using Riftcaster.Core.Network;
using Riftcaster.Core.Overlays;
using Riftcaster.Core.Players;
using Riftcaster.Core.Timer;
using Riftcaster.Server.Api;

namespace Riftcaster.Server;

public partial class Program
{
    /// <summary>
    /// How long stopping waits for what's still open, such as an admin dashboard's connection,
    /// before closing it. The default, 30 seconds, kept Quit, Ctrl+C and Restart to update waiting
    /// that long whenever a dashboard was open: it reconnects as the server starts stopping, and
    /// its connection never ends by itself. Nothing needs longer: overlay sockets close within 2
    /// seconds, and saved state is written as it changes.
    /// </summary>
    internal static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(3);

    private static void Main(string[] args) => CreateApp(args).Run();

    /// <summary>
    /// Creates the server, with all its services, middleware and endpoints, ready to run. The console
    /// server (Main) runs it; the Windows launcher (Riftcaster.Windows, #68) starts it behind a tray icon.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>The created web application.</returns>
    public static WebApplication CreateApp(string[] args)
    {
        // A published build reads and writes everything (appsettings.json, the overlays, data/) in
        // its own folder, wherever it's started from: a shortcut or a terminal elsewhere would
        // otherwise make the current folder the content root, and the server would find none of it.
        // Development keeps the default, the current folder, which Visual Studio and dotnet run set
        // to the project folder.
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = IsDevelopment(args) ? null : AppContext.BaseDirectory,
            // The server's own name, not the running program's: started by the Windows launcher, that's
            // Riftcaster, and the server would look for Riftcaster.staticwebassets.endpoints.json.
            ApplicationName = typeof(Program).Assembly.GetName().Name,
        });

        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = ShutdownTimeout);

        // Network access: off unless started with --network on (see NetworkAccess).
        if (!NetworkAccess.TryParseOption(builder.Configuration["network"], out var networkOn))
        {
            Console.Error.WriteLine($"Unknown --network value '{builder.Configuration["network"]}': use on or off. Starting with network access off.");
        }

        var network = new NetworkAccess(networkOn, NetworkAccess.ParseUrls(builder.Configuration["urls"]));
        builder.Services.AddSingleton(network);
        builder.Configuration.Sources.Add(new NetworkEndpointsSource(network));

        builder.AddOverlays();
        builder.AddRestApi();
        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton(new ServerIdentity("Riftcaster Server", AppVersion(), DateTimeOffset.Now));

        // Saved state: the user's app-data folder in a published build, a data folder of its own in a
        // portable copy, the project's data folder in Development (see DataFolder).
        var dataDirectory = DataFolder.Resolve(
            builder.Configuration[DataFolder.Setting],
            builder.Environment,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

        builder.Logging.AddLogFiles(dataDirectory);

        // Encrypts the saved access code and signs the sign-in cookie. A fixed application name,
        // so they still work if the app's folder moves.
        builder.Services.AddDataProtection().SetApplicationName("Riftcaster");

        builder.Services.AddSingleton<IAccessCodeStore>(services => new ProtectedAccessCodeStore(
            Path.Combine(dataDirectory, "accessCode.json"),
            services.GetRequiredService<IDataProtectionProvider>(),
            services.GetRequiredService<ILogger<ProtectedAccessCodeStore>>()));
        builder.Services.AddSingleton<AccessCode>();
        builder.Services.AddOperatorSignIn();

        builder.Services.AddSingleton<ILowerThirdStore>(services => new JsonFileLowerThirdStore(
            Path.Combine(dataDirectory, "lowerThird.json"),
            services.GetRequiredService<ILogger<JsonFileLowerThirdStore>>()));
        builder.Services.AddSingleton<LowerThirdService>();

        builder.Services.AddSingleton<IMatchStore>(services => new JsonFileMatchStore(
            Path.Combine(dataDirectory, "match.json"),
            services.GetRequiredService<ILogger<JsonFileMatchStore>>()));
        builder.Services.AddSingleton<MatchService>();

        builder.Services.AddSingleton<IPlayersStore>(services => new JsonFilePlayersStore(
            Path.Combine(dataDirectory, "players.json"),
            services.GetRequiredService<ILogger<JsonFilePlayersStore>>()));
        builder.Services.AddSingleton<PlayersService>();

        builder.Services.AddSingleton<OverlayConnections>();

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TimerService>();

        builder.Services.AddSingleton<ICardStore>(services => new JsonFileCardStore(
            Path.Combine(dataDirectory, "cards.json"),
            services.GetRequiredService<ILogger<JsonFileCardStore>>()));
        builder.Services.AddSingleton<ICardSource>(services => new RiftcodexCardSource(
            new HttpClient(new SocketsHttpHandler
            {
                // The client lives as long as the app, so refresh its connections now and then
                // to pick up DNS changes.
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            })
            {
                BaseAddress = new Uri("https://api.riftcodex.com/"),
                // Per request: one page of 100 cards can take half a minute.
                Timeout = TimeSpan.FromMinutes(2),
                // Riftcodex refuses requests without a User-Agent.
                DefaultRequestHeaders =
                {
                    UserAgent = { new ProductInfoHeaderValue("Riftcaster", services.GetRequiredService<ServerIdentity>().Version) },
                },
            },
            services.GetRequiredService<TimeProvider>(),
            services.GetRequiredService<ILogger<RiftcodexCardSource>>()));
        builder.Services.AddSingleton<CardCatalog>();
        builder.Services.AddHostedService<CardCatalogRefresher>();

        builder.Services.AddSingleton<IFeaturedCardStore>(services => new JsonFileFeaturedCardStore(
            Path.Combine(dataDirectory, "featuredCard.json"),
            services.GetRequiredService<ILogger<JsonFileFeaturedCardStore>>()));
        builder.Services.AddSingleton<FeaturedCardService>();

        var app = builder.Build();

        // Say where other devices can reach the server: at startup, and whenever the switch changes.
        var accessCode = app.Services.GetRequiredService<AccessCode>();
        app.Lifetime.ApplicationStarted.Register(() => LogNetworkAccess(app.Logger, network, accessCode));
        app.Logger.LogInformation("Saved data is kept in {Folder}.", dataDirectory);
        network.Changed += () => LogNetworkAccess(app.Logger, network, accessCode);

        app.UseHostCheck(); // First: requests addressed to other names (DNS rebinding) get nothing
        app.UseCrossSiteRequestLimits(); // Before anything acts on an API request
        app.UseAuthentication(); // Reads the sign-in cookie, which the remote access limits check
        app.UseRemoteAccessLimits();
        app.UseRateLimiter(); // Sign-in attempts
        app.UseOverlays();
        app.UseAntiforgery();
        app.UseWebSockets();
        app.MapGet("/api/info", (ServerIdentity identity) => identity)
            .WithTags("Server").WithSummary("Get the server's name, version and start time");
        app.MapOperatorSignIn();
        app.MapFeaturedCard();
        app.MapLowerThird();
        app.MapMatch();
        app.MapPlayers();
        app.MapTimer();
        app.MapRestApiDocs();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        return app;
    }

    /// <summary>
    /// The version this build was given: the one in Directory.Build.props locally, or a release's tag.
    /// The SDK adds the commit to the informational version ("0.2.0+1a2b3c…"); only the version is kept.
    /// </summary>
    private static string AppVersion()
    {
        var version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        return version.Split('+')[0];
    }

    /// <summary>
    /// Whether the app will run in the Development environment, worked out before the builder is
    /// created, the same way it is: from --environment, else ASPNETCORE_ENVIRONMENT, else
    /// DOTNET_ENVIRONMENT. Without any of them, it's Production.
    /// </summary>
    private static bool IsDevelopment(string[] args)
    {
        var environment = new ConfigurationBuilder()
            .AddEnvironmentVariables("DOTNET_")
            .AddEnvironmentVariables("ASPNETCORE_")
            .AddCommandLine(args)
            .Build()[HostDefaults.EnvironmentKey];

        return string.Equals(environment, Environments.Development, StringComparison.OrdinalIgnoreCase);
    }

    private static void LogNetworkAccess(ILogger logger, NetworkAccess network, AccessCode accessCode)
    {
        if (!network.Enabled)
        {
            logger.LogInformation("Network access is off: only this computer can reach the server. Turn it on from the admin dashboard, or start with --network on.");
            return;
        }

        var urls = network.NetworkUrls();
        if (urls.Count == 0)
        {
            logger.LogWarning("Network access is on, but this computer has no network address. Check it's connected to the network.");
            return;
        }

        logger.LogInformation(
            "Network access is on. Other devices can open the overlays at {Urls} (followed by /overlays/...). {Admin}",
            string.Join(" or ", urls),
            accessCode.IsSet
                ? "Approved operators can sign in to the admin dashboard there with the access code."
                : "The admin dashboard stays on this computer until an access code is set.");
    }
}
