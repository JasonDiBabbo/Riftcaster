using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Riftcaster.Core.LowerThird;
using Riftcaster.Core.Match;

namespace Riftcaster.Server.Tests;

/// <summary>
/// Starts the real server for integration tests, but with the lower third library kept in memory,
/// so tests never read or write the real data file (or each other's leftovers).
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
        });
    }
}
