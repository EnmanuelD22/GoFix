using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using GoFix.Application.Modules.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace GoFix.Api.Modules.Identity;

internal static class IdentityHttpRegistration
{
    internal const string AccessPolicy = "identity-access";

    internal static void AddIdentityHttp(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = "__Host-GoFix.Session";
                options.Cookie.Path = "/";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = async context =>
                {
                    // Liveness debe responder aunque SQL esté caído y el cliente envíe su cookie.
                    if (context.HttpContext.Request.Path.StartsWithSegments("/health"))
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    var store = context.HttpContext.RequestServices.GetRequiredService<IIdentityStore>();
                    if (!IdentityEndpoints.TryGetSession(context.Principal, out var userId, out var sessionId) ||
                        !await store.HasSessionAsync(userId, sessionId, context.HttpContext.RequestAborted))
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    }
                };
            });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AccessPolicy, context =>
            {
                var ip = context.Connection.RemoteIpAddress;
                if (ip?.IsIPv4MappedToIPv6 == true)
                    ip = ip.MapToIPv4();
                return RateLimitPartition.GetFixedWindowLimiter(ip?.ToString() ?? "unknown", _ => new()
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
            options.OnRejected = (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var remaining))
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(remaining.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                return ValueTask.CompletedTask;
            };
        });
    }

    internal static void UseTrustedClientAddresses(this WebApplication app)
    {
        var proxies = app.Configuration.GetSection("ReverseProxy:KnownAddresses").Get<string[]>() ?? [];
        if (proxies.Length == 0)
            return;

        // Usar solo con un ingreso que sobrescriba X-Forwarded-For y proteja el acceso directo al proxy.
        var options = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor, ForwardLimit = 1 };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var address in proxies)
            options.KnownProxies.Add(IPAddress.Parse(address));
        app.UseForwardedHeaders(options);
    }
}
