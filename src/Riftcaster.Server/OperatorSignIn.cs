using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Riftcaster.Core.Network;

namespace Riftcaster.Server;

/// <summary>
/// Signing in remote operators with the access code (issue #16): a small sign-in page, and a
/// cookie that keeps the device signed in for as long as the code it used is current.
/// </summary>
/// <remarks>
/// The cookie doesn't contain the code: it's an encrypted, signed ticket holding the code's
/// <see cref="AccessCode.Version"/>. Changing or removing the code changes the version, so every
/// existing cookie stops counting. Devices on this computer never need to sign in.
/// </remarks>
internal static class OperatorSignIn
{
    /// <summary>
    /// The sign-in page, and where its form posts.
    /// </summary>
    public const string SignInPath = "/signin";

    /// <summary>
    /// Where the admin's Sign out button posts.
    /// </summary>
    public const string SignOutPath = "/signout";

    /// <summary>
    /// The longest a device stays signed in, unless the code changes first: long enough that, in
    /// practice, a device stays signed in for as long as the code it used.
    /// </summary>
    public static readonly TimeSpan CookieLifetime = TimeSpan.FromDays(365);

    /// <summary>
    /// How many sign-in attempts one device may make per <see cref="AttemptWindow"/>.
    /// </summary>
    public const int AttemptLimit = 5;

    /// <summary>
    /// See <see cref="AttemptLimit"/>.
    /// </summary>
    public static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(1);

    private const string CodeVersionClaim = "riftcaster:code-version";

    private const string AttemptsPolicy = "signin-attempts";

    /// <summary>
    /// Registers the sign-in cookie and the limit on sign-in attempts.
    /// </summary>
    public static IServiceCollection AddOperatorSignIn(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "Riftcaster.Operator";
                options.Cookie.HttpOnly = true; // Page scripts can't read it
                options.Cookie.SameSite = SameSiteMode.Lax; // Other sites can't make the browser use it
                options.ExpireTimeSpan = CookieLifetime;
                options.SlidingExpiration = false;

                // A sign-in only counts while the code it used is current.
                options.Events.OnValidatePrincipal = context =>
                {
                    var accessCode = context.HttpContext.RequestServices.GetRequiredService<AccessCode>();
                    var version = context.Principal?.FindFirstValue(CodeVersionClaim);
                    if (accessCode.Version is null || version != accessCode.Version)
                    {
                        context.RejectPrincipal();
                    }

                    return Task.CompletedTask;
                };
            });

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AttemptsPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = AttemptLimit, Window = AttemptWindow, QueueLimit = 0 }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await WritePageAsync(context.HttpContext, form: null, "Too many attempts. Wait a minute, then try again.");
            };
        });

        return services;
    }

    /// <summary>
    /// Maps the sign-in page, the sign-in form's post, and sign-out.
    /// </summary>
    public static WebApplication MapOperatorSignIn(this WebApplication app)
    {
        // Pages, not part of the REST API, so left out of its OpenAPI document.
        app.MapGet(SignInPath, async (HttpContext context, AccessCode accessCode, IAntiforgery antiforgery) =>
        {
            if (NetworkAccess.IsLocal(context.Connection.RemoteIpAddress) || context.User.Identity?.IsAuthenticated == true)
            {
                return Results.Redirect("/"); // Nothing to sign in to
            }

            await WritePageAsync(context, accessCode.IsSet ? antiforgery.GetAndStoreTokens(context) : null, message: null);
            return Results.Empty;
        }).ExcludeFromDescription();

        app.MapPost(SignInPath, async (HttpContext context, [FromForm] string? code, AccessCode accessCode, IAntiforgery antiforgery) =>
        {
            if (!accessCode.Matches(code))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await WritePageAsync(
                    context,
                    accessCode.IsSet ? antiforgery.GetAndStoreTokens(context) : null,
                    accessCode.IsSet ? "That code isn't right. Check it with whoever runs the server." : null);
                return Results.Empty;
            }

            var identity = new ClaimsIdentity([new Claim(CodeVersionClaim, accessCode.Version!)], CookieAuthenticationDefaults.AuthenticationScheme);
            await context.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
            return Results.Redirect("/");
        }).RequireRateLimiting(AttemptsPolicy).ExcludeFromDescription();

        app.MapPost(SignOutPath, async (HttpContext context) =>
        {
            await context.SignOutAsync();
            return Results.Redirect(SignInPath);
        }).ExcludeFromDescription();

        return app;
    }

    /// <summary>
    /// Writes the sign-in page: plain HTML, not part of the Blazor admin, since a device that
    /// hasn't signed in mustn't open the admin's live connection.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <param name="form">Antiforgery tokens for the form, or <see langword="null"/> for no form (no code is set, or too many attempts).</param>
    /// <param name="message">An error to show, or <see langword="null"/>.</param>
    private static async Task WritePageAsync(HttpContext context, AntiforgeryTokenSet? form, string? message)
    {
        var accessCode = context.RequestServices.GetRequiredService<AccessCode>();
        var body = !accessCode.IsSet
            ? """<p>Remote access isn't set up. To use the admin from this device, set an access code in the admin on the computer running the server.</p>"""
            : form is null
                ? ""
                : $"""
                  <form method="post" action="{SignInPath}">
                    <input type="hidden" name="{WebUtility.HtmlEncode(form.FormFieldName)}" value="{WebUtility.HtmlEncode(form.RequestToken)}" />
                    <label for="code">Access code</label>
                    <input id="code" name="code" type="text" autocomplete="off" autocapitalize="characters" spellcheck="false" required autofocus />
                    <button type="submit">Sign in</button>
                  </form>
                  """;

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync($$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Sign in · Riftcaster Admin</title>
              <style>
                @font-face { font-family: 'DM Sans'; font-weight: 100 1000; src: url('/_content/Riftcaster.Admin/fonts/dm-sans-latin-opsz-normal.woff2') format('woff2'); }
                * { box-sizing: border-box; }
                html { color-scheme: dark; }
                body { margin: 0; min-height: 100vh; display: grid; place-items: center; padding: 16px; background: #0e0f11; color: #e8e6e1; font: 400 14px / 1.45 'DM Sans', sans-serif; }
                main { width: min(380px, 100%); display: flex; flex-direction: column; gap: 14px; padding: 22px 20px; border: 1px solid #2c3036; border-radius: 10px; background: #16181b; }
                h1 { margin: 0; color: oklch(0.78 0.12 165); font: 700 20px / 1 'DM Sans', sans-serif; letter-spacing: 0.05em; }
                p { margin: 0; color: #9a9da3; }
                .error { color: #f08a84; }
                form { display: flex; flex-direction: column; gap: 10px; }
                label { color: #9a9da3; font: 500 11px / 1 'DM Sans', sans-serif; letter-spacing: 0.06em; text-transform: uppercase; }
                input[type=text] { height: 44px; padding: 0 12px; border: 1px solid #2c3036; border-radius: 6px; background: #0f1113; color: #f3f1ec; font: 600 18px / 1 ui-monospace, Consolas, monospace; letter-spacing: 0.08em; }
                input[type=text]:focus { border-color: oklch(0.78 0.12 165); outline: none; }
                button { height: 44px; border: none; border-radius: 6px; background: oklch(0.78 0.12 165); color: #0d0f12; font: 700 14px / 1 'DM Sans', sans-serif; cursor: pointer; }
                @media (hover: hover) { button:hover { background: oklch(0.84 0.12 165); } }
                button:active { filter: brightness(0.88); }
                button:focus-visible { outline: 2px solid oklch(0.78 0.12 165); outline-offset: 2px; }
              </style>
            </head>
            <body>
              <main>
                <h1>RIFTCASTER</h1>
                <p>Enter the access code to use the admin on this device. Whoever runs the server can give it to you.</p>
                {{(message is null ? "" : $"""<p class="error" role="alert">{WebUtility.HtmlEncode(message)}</p>""")}}
                {{body}}
              </main>
            </body>
            </html>
            """);
    }
}
