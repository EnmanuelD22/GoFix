using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;

namespace GoFix.Api;

internal static class HttpConfiguration
{
    #region Contrato HTTP

    internal static void AddGoFixHttp(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                var details = context.ProblemDetails;
                details.Extensions.TryAdd("traceId",
                    Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
                // Los fallos del transporte tienen un código estable, independiente de los casos de uso.
                details.Extensions.TryAdd("code", $"http_{details.Status}");
            };
        });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-GoFix.Antiforgery";
            options.Cookie.Path = "/";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        });
    }

    internal static void UseGoFixErrors(this WebApplication app)
    {
        app.UseExceptionHandler(errorApp => errorApp.Run(context =>
            Results.Problem(statusCode: StatusCodes.Status500InternalServerError).ExecuteAsync(context)));

        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), api =>
        {
            api.UseStatusCodePages(status => Results.Problem(
                statusCode: status.HttpContext.Response.StatusCode).ExecuteAsync(status.HttpContext));
        });
    }

    internal static void MapGoFixApi(this RouteGroupBuilder api)
    {
        api.AddEndpointFilter(ValidateMutationAsync);
        api.MapGet("/auth/csrf", IssueAntiforgeryToken).WithName("IssueAntiforgeryToken");
    }

    private static async ValueTask<object?> ValidateMutationAsync(
        EndpointFilterInvocationContext invocation,
        EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        var method = context.Request.Method;
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) && !HttpMethods.IsOptions(method))
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                // No se exponen detalles del token, de la cookie ni de la identidad.
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest);
            }
        }

        return await next(invocation);
    }

    private static AntiforgeryTokenResponse IssueAntiforgeryToken(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return new(tokens.RequestToken
            ?? throw new InvalidOperationException("No se generó el token antifalsificación."));
    }

    internal sealed record AntiforgeryTokenResponse(string Token);

    #endregion

    #region Aplicación web de origen único

    internal static void UseGoFixStaticFiles(this WebApplication app)
    {
        if (Directory.Exists(app.Environment.WebRootPath))
        {
            app.UseWhen(context => !IsReservedPath(context.Request.Path), browser => browser.UseStaticFiles());
        }
    }

    internal static void MapGoFixSpa(this WebApplication app)
    {
        var index = app.Environment.WebRootFileProvider.GetFileInfo("index.html");
        app.MapFallback((HttpContext context) =>
            !IsReservedPath(context.Request.Path) && index.PhysicalPath is { } indexPath && File.Exists(indexPath)
                ? Results.File(indexPath, "text/html; charset=utf-8")
                : Results.NotFound())
            .ExcludeFromDescription();
    }

    // Las rutas reservadas conservan sus respuestas HTTP aunque React publique una ruta con el mismo nombre.
    private static bool IsReservedPath(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/health") || path.StartsWithSegments("/openapi");

    #endregion
}
