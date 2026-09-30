using Riftcaster.Admin;
using Riftcaster.Contracts;
using Riftcaster.Core.LowerThird;
using Riftcaster.Core.Match;

namespace Riftcaster.Server;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton(new ServerIdentity("Riftcaster Server", "0.1.0", DateTimeOffset.Now));

        var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["Storage:DataDirectory"] ?? "data");
        builder.Services.AddSingleton<ILowerThirdStore>(services => new JsonFileLowerThirdStore(
            Path.Combine(dataDirectory, "lowerThird.json"),
            services.GetRequiredService<ILogger<JsonFileLowerThirdStore>>()));
        builder.Services.AddSingleton<LowerThirdService>();

        builder.Services.AddSingleton<IMatchStore>(services => new JsonFileMatchStore(
            Path.Combine(dataDirectory, "match.json"),
            services.GetRequiredService<ILogger<JsonFileMatchStore>>()));
        builder.Services.AddSingleton<MatchService>();

        var app = builder.Build();

        app.UseOverlays();
        app.UseAntiforgery();
        app.MapGet("/api/info", (ServerIdentity identity) => identity);
        app.MapLowerThird();
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        app.Run();
    }
}
