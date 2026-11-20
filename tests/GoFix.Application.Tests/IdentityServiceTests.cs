using GoFix.Application.Modules.Identity;
using Xunit;

namespace GoFix.Application.Tests;

public sealed class IdentityServiceTests
{
    [Fact]
    public async Task RegistroInvalidoRechazaRolYDatosAntesDePersistir()
    {
        var store = new RegistrationStore();
        var service = new IdentityService(store);

        var errors = await service.RegisterAsync(
            new("<a>", "incorrecto", "123", "corta", "admin"), CancellationToken.None);

        Assert.NotNull(errors);
        Assert.Equal(new[] { "contrasena", "correo", "nombre", "rol", "telefono" }, errors.Keys.Order());
        Assert.Null(store.ReceivedRegistration);
    }

    [Fact]
    public async Task RegistroRecortaLosDatosYConservaLaContrasenaExacta()
    {
        var store = new RegistrationStore();
        var service = new IdentityService(store);
        const string password = " ClaveDePruebas2026 ";

        var errors = await service.RegisterAsync(new("  Ana Prueba  ", " ana@example.test ",
            " +59176543210 ", password, "tecnico"), CancellationToken.None);

        Assert.Null(errors);
        Assert.Equal(new Registration("Ana Prueba", "ana@example.test", "+59176543210", password, "tecnico"),
            store.ReceivedRegistration);
    }

    private sealed class RegistrationStore : IIdentityStore
    {
        public Registration? ReceivedRegistration { get; private set; }

        public Task<bool> RegisterAsync(Registration input, CancellationToken cancellationToken)
        {
            ReceivedRegistration = input;
            return Task.FromResult(true);
        }

        public Task<Session?> AuthenticateAsync(Credentials input, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Account?> FindAccountAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Account?> UpdateAsync(Guid userId, AccountDetails input, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
