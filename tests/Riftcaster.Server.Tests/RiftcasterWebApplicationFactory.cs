using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Riftcaster.Core.Cards;
using Riftcaster.Core.FeaturedCard;
using Riftcaster.Core.LowerThird;
using Riftcaster.Core.Match;
using Riftcaster.Core.Network;
using Riftcaster.Core.Players;

namespace Riftcaster.Server.Tests;

/// <summary>
/// Starts the real server for integration tests, but with every store kept in memory, so tests
/// never read or write the real data files (or each other's leftovers), and a card source that
/// never calls the real card database.
/// </summary>
public class RiftcasterWebApplicationFactory : WebApplicationFactory<Program>
{
    // Its own data folder, for its log files (see LogFiles): not the project's, among the real ones.
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("riftcaster-tests-").FullName;

    /// <summary>
    /// Disposes the server and its log file, then deletes the temporary data folder.
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync(); // Stops the server, closing its log file

        // Something outside the tests, such as antivirus scanning the new log file, can have it open
        // for a moment after the server closes it, mostly while every test class starts at once. A
        // file the server itself left open still fails, after a second.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(_dataDirectory, recursive: true);
                break;
            }
            catch (IOException) when (attempt < 10)
            {
                await Task.Delay(100);
            }
        }

        GC.SuppressFinalize(this);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(DataFolder.Setting, _dataDirectory);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILowerThirdStore>();
            services.AddSingleton<ILowerThirdStore, InMemoryLowerThirdStore>();

            services.RemoveAll<IMatchStore>();
            services.AddSingleton<IMatchStore, InMemoryMatchStore>();

            services.RemoveAll<IPlayersStore>();
            services.AddSingleton<IPlayersStore, InMemoryPlayersStore>();

            services.RemoveAll<ICardStore>();
            services.AddSingleton<ICardStore, InMemoryCardStore>();

            services.RemoveAll<ICardSource>();
            services.AddSingleton<ICardSource, InMemoryCardSource>();

            services.RemoveAll<IFeaturedCardStore>();
            services.AddSingleton<IFeaturedCardStore, InMemoryFeaturedCardStore>();

            services.RemoveAll<IAccessCodeStore>();
            services.AddSingleton<IAccessCodeStore, InMemoryAccessCodeStore>();

            // Keys that live only as long as the test server, never in the user's own key store.
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        });
    }
}
