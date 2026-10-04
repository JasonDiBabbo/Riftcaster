using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;

namespace Riftcaster.Server;

/// <summary>
/// Answers only requests addressed to this computer (issue #55), which stops DNS rebinding.
/// </summary>
/// <remarks>
/// In a rebinding attack, a website's page changes its own domain's DNS to point at this computer,
/// then calls Riftcaster as that domain. The browser treats the answers as the page's own, and
/// Riftcaster sees requests from 127.0.0.1, which need no access code. Every such request names the
/// attacker's domain in its Host header, so only names that can't be rebound, or that are this
/// computer's own, are answered: localhost (and *.localhost, which browsers keep on this computer),
/// IP addresses, this computer's names, and any added in the HostNames setting.
/// </remarks>
internal static class HostCheck
{
    /// <summary>
    /// The setting for extra names to answer to, separated by semicolons, for example
    /// "riftcaster.lan;obs-pc.example.com".
    /// </summary>
    public const string HostNamesSetting = "HostNames";

    public static IApplicationBuilder UseHostCheck(this WebApplication app)
    {
        var allowed = AllowedNames(app.Configuration[HostNamesSetting]);
        var reported = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        return app.Use(async (context, next) =>
        {
            var host = context.Request.Host;
            if (IsAllowed(host, allowed))
            {
                await next(context);
                return;
            }

            // Once per name: a rebinding page might send a request a second.
            if (reported.TryAdd(host.Host, true))
            {
                app.Logger.LogWarning(
                    "Refused requests addressed to '{Host}'. If that's a name for this computer, add it to the {Setting} setting.",
                    host.Host, HostNamesSetting);
            }

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(
                $"Riftcaster only answers requests addressed to this computer: localhost, its IP address, or its name. To reach it by another name, add the name to the {HostNamesSetting} setting.");
        });
    }

    /// <summary>
    /// Whether a request addressed to this host should be answered.
    /// </summary>
    /// <param name="host">The request's Host header.</param>
    /// <param name="allowed">This computer's names and the configured ones (see <see cref="AllowedNames"/>).</param>
    internal static bool IsAllowed(HostString host, IReadOnlySet<string> allowed)
    {
        var name = host.Host.TrimEnd('.'); // "localhost." is the same name, written in full
        if (name.Length == 0)
        {
            return false;
        }

        return IPAddress.TryParse(name.Trim('[', ']'), out _)
            || name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || allowed.Contains(name);
    }

    /// <summary>
    /// This computer's names, and the extra ones from the setting, ignoring case.
    /// </summary>
    /// <param name="configured">The HostNames setting, or null.</param>
    internal static IReadOnlySet<string> AllowedNames(string? configured)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // The DNS host name, and Windows' shorter NetBIOS name, which can differ (it's at most 15
        // characters), each as itself, on the local network (mDNS's .local), and with the domain.
        var domain = IPGlobalProperties.GetIPGlobalProperties().DomainName;
        foreach (var computer in new[] { Dns.GetHostName(), Environment.MachineName })
        {
            names.Add(computer);
            names.Add($"{computer}.local");
            if (!string.IsNullOrEmpty(domain))
            {
                names.Add($"{computer}.{domain}");
            }
        }

        foreach (var extra in (configured ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            names.Add(extra.TrimEnd('.'));
        }

        return names;
    }
}
