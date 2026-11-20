using System.Security.Claims;
using GoFix.Application.Modules.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace GoFix.Api.Modules.Identity;

internal static class IdentityEndpoints
{
    private const string SessionClaim = "gofix:session_id";

    internal static void MapIdentityEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Identidad");
        auth.MapPost("/register", RegisterAsync).RequireRateLimiting(IdentityHttpRegistration.AccessPolicy)
            .Produces(StatusCodes.Status201Created).ProducesValidationProblem().ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/login", LoginAsync).RequireRateLimiting(IdentityHttpRegistration.AccessPolicy)
            .Produces<Account>().ProducesProblem(StatusCodes.Status401Unauthorized).ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization().Produces(StatusCodes.Status204NoContent);
        auth.MapGet("/me", GetAccountAsync).RequireAuthorization().Produces<Account>().ProducesProblem(StatusCodes.Status401Unauthorized);
        api.MapPut("/cuenta", UpdateAccountAsync).WithTags("Identidad").RequireAuthorization()
            .Produces<Account>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    #region Operaciones HTTP

    private static async Task<IResult> RegisterAsync(Registration input, IdentityService identity, CancellationToken cancellationToken)
    {
        var errors = await identity.RegisterAsync(input, cancellationToken);
        return errors is null ? Results.StatusCode(StatusCodes.Status201Created) : Results.ValidationProblem(errors);
    }

    private static async Task<IResult> LoginAsync(
        Credentials input, HttpContext context, IdentityService identity, CancellationToken cancellationToken)
    {
        var session = await identity.LoginAsync(input, cancellationToken);
        if (session is null)
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized,
                title: "No pudimos iniciar sesión. Revisa tus datos o inténtalo más tarde.");

        var account = session.Cuenta;
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Role, account.Rol),
            new(SessionClaim, session.Id.ToString())
        ], IdentityConstants.ApplicationScheme));

        await context.SignInAsync(IdentityConstants.ApplicationScheme, principal,
            new AuthenticationProperties { ExpiresUtc = session.ExpiresAt, IsPersistent = false });
        return PrivateAccount(context, account);
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context, IIdentityStore store, CancellationToken cancellationToken)
    {
        if (!TryGetSession(context.User, out var userId, out var sessionId))
            return Results.Unauthorized();

        await store.RevokeSessionAsync(userId, sessionId, cancellationToken);
        await context.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetAccountAsync(
        HttpContext context, IdentityService identity, CancellationToken cancellationToken)
    {
        if (!TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();

        var account = await identity.GetAccountAsync(userId, cancellationToken);
        return account is null ? Results.Unauthorized() : PrivateAccount(context, account);
    }

    private static async Task<IResult> UpdateAccountAsync(
        AccountDetails input, HttpContext context, IdentityService identity, CancellationToken cancellationToken)
    {
        if (!TryGetSession(context.User, out var userId, out _))
            return Results.Unauthorized();

        var result = await identity.UpdateAccountAsync(userId, input, cancellationToken);
        if (result.Errors is not null)
            return Results.ValidationProblem(result.Errors);
        return result.Account is null ? Results.Unauthorized() : PrivateAccount(context, result.Account);
    }

    #endregion

    internal static bool TryGetSession(ClaimsPrincipal? principal, out Guid userId, out Guid sessionId)
    {
        var validUser = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
        var validSession = Guid.TryParse(principal?.FindFirstValue(SessionClaim), out sessionId);
        return validUser && validSession;
    }

    private static IResult PrivateAccount(HttpContext context, Account account)
    {
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(account);
    }
}
