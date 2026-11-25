using System.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;

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
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GoFix.Api.Errors");
                logger.LogWarning(new EventId(1001, "ApiProblem"),
                    "API error {StatusCode} {Code} {TraceId}", details.Status,
                    details.Extensions["code"], details.Extensions["traceId"]);
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
        {
            // El binding de HTTP puede rechazar la entrada antes de alcanzar el caso de uso.
            // Conservamos ese código sin exponer el contenido de la petición ni la excepción.
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var status = exception is BadHttpRequestException badRequest
                ? badRequest.StatusCode
                : StatusCodes.Status500InternalServerError;
            if (exception is not null)
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GoFix.Api.Errors");
                logger.LogError(new EventId(1002, "ApiException"),
                    "API exception {ExceptionType} {TraceId}", exception.GetType().FullName,
                    Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier);
            }
            return Results.Problem(statusCode: status).ExecuteAsync(context);
        }));

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

}
