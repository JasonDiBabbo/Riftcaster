using System.Diagnostics;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Riftcaster.Server.Tests;

public class ShutdownTests(RiftcasterWebApplicationFactory factory) : IClassFixture<RiftcasterWebApplicationFactory>
{
    [Fact]
    public void ShutdownTimeout_IsAFewSeconds()
    {
        var options = factory.Services.GetRequiredService<IOptions<HostOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(3), options.ShutdownTimeout);
    }

    [Fact]
    public async Task Stopping_GivesUpOnWhatsStillOpen_AfterTheShutdownTimeout()
    {
        // Like an admin dashboard's connection, which doesn't end by itself: a service that only
        // stops when the server gives up waiting for it.
        // Its own server, since this one is stopped; the factory it comes from deletes its data folder.
        await using var serverFactory = new RiftcasterWebApplicationFactory();
        var server = serverFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddHostedService<NeverStopsByItself>()));
        _ = server.Services; // Starts the server

        var stopping = Stopwatch.StartNew();
        await server.DisposeAsync();
        stopping.Stop();

        // The timer that ends the wait and this stopwatch don't tick in step. The wait can measure a
        // little under the timeout: 2.99999 seconds once on CI. Allowing half a second under the
        // timeout still shows that stopping waited for it, and didn't wait the default 30 seconds.
        Assert.InRange(stopping.Elapsed, Program.ShutdownTimeout - TimeSpan.FromSeconds(0.5), Program.ShutdownTimeout + TimeSpan.FromSeconds(5));
    }

    private sealed class NeverStopsByItself : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        // Cancelled when the shutdown timeout runs out.
        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.Delay(Timeout.Infinite, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
    }
}
