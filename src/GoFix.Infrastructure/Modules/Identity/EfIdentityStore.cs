using GoFix.Application.Modules.Identity;
using GoFix.Domain.Modules.Professionals;
using GoFix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Modules.Identity;

internal sealed class EfIdentityStore(
    GoFixDbContext db,
    UserManager<GoFixUser> users,
    SignInManager<GoFixUser> signIn,
    TimeProvider clock) : IIdentityStore
{
    private static readonly string UnknownUserPasswordHash =
        new PasswordHasher<GoFixUser>().HashPassword(new GoFixUser(), Guid.NewGuid().ToString());

    #region Cuentas

    public async Task<bool> RegisterAsync(Registration input, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var userId = Guid.NewGuid();
        var user = new GoFixUser
        {
            Id = userId,
            UserName = userId.ToString("D"),
            Email = input.Correo,
            Nombre = input.Nombre!,
            PhoneNumber = input.Telefono
        };

        IdentityResult created;
        try
        {
            created = await users.CreateAsync(user, input.Contrasena!);
        }
        catch (DbUpdateException exception) when (IsEmailCollision(exception))
        {
            // El índice único arbitra registros concurrentes del mismo correo.
            return false;
        }

        if (created.Errors.Any(error => error.Code is "DuplicateEmail"))
            return false;
        EnsureSucceeded(created);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureSucceeded(await users.AddToRoleAsync(user, input.Rol!));

        if (input.Rol == AccountRole.Technician)
        {
            db.PerfilesTecnicos.Add(new PerfilTecnico(user.Id));
            await db.SaveChangesAsync(cancellationToken);
        }

        // Los guardados internos de Identity comparten este contexto y quedan dentro de la transacción.
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public Task<Account?> FindAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        (from user in db.Users.AsNoTracking()
         join userRole in db.UserRoles on user.Id equals userRole.UserId
         join role in db.Roles on userRole.RoleId equals role.Id
         where user.Id == userId
         select new Account(user.Id, user.Nombre, user.Email!, user.PhoneNumber!, role.Name!,
             db.PerfilesTecnicos.Where(profile => profile.UsuarioId == user.Id).Select(profile => (Guid?)profile.Id).FirstOrDefault()))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<Account?> UpdateAsync(Guid userId, AccountDetails input, CancellationToken cancellationToken)
    {
        var stamp = Guid.NewGuid().ToString("D");
        // El sello evita que una escritura de Identity basada en una lectura anterior restaure datos viejos.
        var updated = await db.Users.Where(user => user.Id == userId).ExecuteUpdateAsync(setters => setters
            .SetProperty(user => user.Nombre, input.Nombre!)
            .SetProperty(user => user.PhoneNumber, input.Telefono)
            .SetProperty(user => user.ConcurrencyStamp, stamp), cancellationToken);
        return updated == 0 ? null : await FindAccountAsync(userId, cancellationToken);
    }

    #endregion

    #region Sesiones

    public async Task<Session?> AuthenticateAsync(Credentials input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(input.Correo!);
        if (user is null)
        {
            // Mantiene el costo del hash también para un correo sin cuenta.
            _ = users.PasswordHasher.VerifyHashedPassword(new GoFixUser(), UnknownUserPasswordHash, input.Contrasena!);
            return null;
        }

        var authentication = await signIn.CheckPasswordSignInAsync(user, input.Contrasena!, lockoutOnFailure: true);
        if (!authentication.Succeeded)
            return null;

        var account = await FindAccountAsync(user.Id, cancellationToken);
        if (account is null)
            return null;

        var now = clock.GetUtcNow();
        var session = new IdentitySession { Id = Guid.NewGuid(), UserId = user.Id, ExpiresAt = now.AddMinutes(60) };
        await db.Sessions.Where(existing => existing.UserId == user.Id && existing.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return new(session.Id, account, session.ExpiresAt);
    }

    public Task<bool> HasSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return db.Sessions.AnyAsync(session => session.Id == sessionId &&
            session.UserId == userId && session.ExpiresAt > now, cancellationToken);
    }

    public async Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        // Revocar la fila invalida también cualquier copia previa de la cookie de esa sesión.
        await db.Sessions.Where(session => session.Id == sessionId && session.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    #endregion

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("No se pudo guardar la cuenta o su rol de acceso.");
    }

    private static bool IsEmailCollision(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains("'EmailIndex'", StringComparison.Ordinal);
}
