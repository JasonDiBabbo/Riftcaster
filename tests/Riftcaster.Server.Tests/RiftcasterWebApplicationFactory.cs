using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Riftcaster.Core.Cards;
using Riftcaster.Core.LowerThird;
using Riftcaster.Core.Match;
using Riftcaster.Core.Players;

namespace Riftcaster.Server.Tests;

/// <summary>
/// Starts the real server for integration tests, but with every store kept in memory, so tests
/// never read or write the real data files (or each other's leftovers), and a card source that
/// never calls the real card database.
/// </summary>
public class RiftcasterWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
        });
    }
}
