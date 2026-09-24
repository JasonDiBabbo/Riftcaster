using Riftcaster.Admin;
using Riftcaster.Contracts;

namespace Riftcaster.Server;

public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();
        builder.Services.AddSingleton(new ServerIdentity("Riftcaster Server", "0.1.0", DateTimeOffset.Now));

        var app = builder.Build();

        app.UseOverlays();
        app.UseAntiforgery();
        app.MapGet("/api/info", (ServerIdentity identity) => identity);
        app.MapStaticAssets();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        app.Run();
    }
}