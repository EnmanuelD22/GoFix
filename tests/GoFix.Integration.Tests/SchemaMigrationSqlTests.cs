using GoFix.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace GoFix.Integration.Tests;

public sealed class SchemaMigrationSqlTests : IAsyncLifetime
{
    private readonly string databaseName = $"GoFixSchemaTests_{Guid.NewGuid():N}";
    private readonly GoFixDbContext db;
    private bool ownsDatabase;

    public SchemaMigrationSqlTests()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("GOFIX_TEST_SQLSERVER"))
        {
            InitialCatalog = databaseName,
            AttachDBFilename = string.Empty
        };
        db = new GoFixDbContext(new DbContextOptionsBuilder<GoFixDbContext>().UseSqlServer(connection.ConnectionString).Options);
    }

    #region Base aislada e instalación incremental

    public async Task InitializeAsync()
    {
        Assert.False(await db.Database.CanConnectAsync());
        ownsDatabase = true;
        await db.GetService<IMigrator>().MigrateAsync("InitialIdentity");
    }

    public async Task DisposeAsync()
    {
        await using (db)
        {
            if (ownsDatabase)
                await db.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public async Task ActualizarDesdeIdentidadConservaPerfilYRestringeRelacionesDeCatalogo()
    {
        var tecnico = await CrearPerfilAsync("tecnico");
        var versionAnterior = await LeerVersionPerfilAsync(tecnico);

        await db.Database.MigrateAsync();

        var versionActual = await LeerVersionPerfilAsync(tecnico);
        Assert.Equal(versionAnterior, versionActual);
        var (especialidad, sector) = await CrearCatalogoAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO professionals.TecnicoEspecialidad (PerfilTecnicoId, EspecialidadId) VALUES ({tecnico}, {especialidad});
            INSERT INTO professionals.TecnicoSector (PerfilTecnicoId, SectorId) VALUES ({tecnico}, {sector});
            INSERT INTO professionals.Sector (Codigo, Nombre, Municipio, Activo) VALUES ('barrio-otra-ciudad', N'Centro', N'Otro municipio', 1);
            """);

        await RechazarAsync($"INSERT INTO professionals.Especialidad (Codigo, Nombre, Activa) VALUES ('electricidad', N'Otra especialidad', 1);", 2601, 2627);
        await RechazarAsync($"INSERT INTO professionals.Especialidad (Codigo, Nombre, Activa) VALUES ('electricidad-otra', N'Electricidad', 1);", 2601, 2627);
        await RechazarAsync($"INSERT INTO professionals.Sector (Codigo, Nombre, Municipio, Activo) VALUES ('otro-centro', N'Centro', N'Municipio de prueba', 1);", 2601, 2627);
        await RechazarAsync($"INSERT INTO professionals.TecnicoSector (PerfilTecnicoId, SectorId) VALUES ({tecnico}, {sector});", 2601, 2627);
        await RechazarAsync($"DELETE FROM professionals.Especialidad WHERE Id = {especialidad};", 547);

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE professionals.Especialidad SET Activa = 0 WHERE Id = {especialidad};");
        var asociados = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(*) AS [Value] FROM professionals.TecnicoEspecialidad WHERE PerfilTecnicoId = {tecnico}
            """).SingleAsync();
        Assert.Equal(1, asociados);
    }

    #endregion

    #region Integridad del esquema de servicios

    [SqlServerFact]
    public async Task SolicitudProtegeVersionYCoherenciaDelBorradoLogico()
    {
        await db.Database.MigrateAsync();
        var cliente = await CrearUsuarioAsync("cliente");
        var tecnico = await CrearPerfilAsync("tecnico");
        var (especialidad, sector) = await CrearCatalogoAsync();
        var solicitud = await CrearSolicitudAsync(cliente, tecnico, especialidad, sector, 1);
        var cita = await db.Database.SqlQuery<DateTimeOffset>($"""
            SELECT FechaPreferida AS [Value] FROM services.SolicitudServicio WHERE Id = {solicitud}
            """).SingleAsync();
        Assert.Equal(TimeSpan.FromHours(-4), cita.Offset);

        await RechazarAsync(InsertarHistorial(solicitud, Guid.NewGuid()), 547);
        await db.Database.ExecuteSqlInterpolatedAsync(InsertarHistorial(solicitud, cliente));
        var versiones = db.Database.SqlQuery<byte[]>($"SELECT [RowVersion] AS [Value] FROM services.SolicitudServicio WHERE Id = {solicitud}");
        var versionInicial = await versiones.SingleAsync();
        Assert.Equal(8, versionInicial.Length);

        await RechazarAsync($"UPDATE services.SolicitudServicio SET IsDeleted = 1 WHERE Id = {solicitud};", 547);
        var actualizado = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE services.SolicitudServicio SET IsDeleted = 1, DeletedAtUtc = SYSUTCDATETIME()
            WHERE Id = {solicitud} AND [RowVersion] = {versionInicial};
            """);
        var versionActual = await versiones.SingleAsync();
        var desactualizado = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE services.SolicitudServicio SET Titulo = N'Edición concurrente'
            WHERE Id = {solicitud} AND [RowVersion] = {versionInicial};
            """);

        Assert.Equal(1, actualizado);
        Assert.False(versionInicial.SequenceEqual(versionActual));
        Assert.Equal(0, desactualizado);
    }

    [SqlServerFact]
    public async Task ValoracionesEIdempotenciaPertenecenALaSolicitudYSonUnicas()
    {
        await db.Database.MigrateAsync();
        var cliente = await CrearUsuarioAsync("cliente");
        var otroCliente = await CrearUsuarioAsync("otro-cliente");
        var tecnico = await CrearPerfilAsync("tecnico");
        var otroTecnico = await CrearPerfilAsync("otro-tecnico");
        var (especialidad, sector) = await CrearCatalogoAsync();
        var solicitud = await CrearSolicitudAsync(cliente, tecnico, especialidad, sector, 6);

        await RechazarAsync(InsertarValoracion(solicitud, otroCliente, tecnico, 5), 547);
        await RechazarAsync(InsertarValoracion(solicitud, cliente, otroTecnico, 5), 547);
        await RechazarAsync(InsertarValoracion(solicitud, cliente, tecnico, 6), 547);
        await db.Database.ExecuteSqlInterpolatedAsync(InsertarValoracion(solicitud, cliente, tecnico, 5));
        await RechazarAsync(InsertarValoracion(solicitud, cliente, tecnico, 4), 2601, 2627);

        var clave = Guid.NewGuid();
        await RechazarAsync(InsertarIdempotencia(solicitud, otroCliente, clave), 547);
        await db.Database.ExecuteSqlInterpolatedAsync(InsertarIdempotencia(solicitud, cliente, clave));
        await RechazarAsync(InsertarIdempotencia(solicitud, cliente, Guid.NewGuid()), 2601, 2627);

        var segundaSolicitud = await CrearSolicitudAsync(cliente, tecnico, especialidad, sector, 1);
        await RechazarAsync(InsertarIdempotencia(segundaSolicitud, cliente, clave), 2601, 2627);
    }

    #endregion

    #region Datos sintéticos para las pruebas

    private async Task<Guid> CrearUsuarioAsync(string alias)
    {
        var id = Guid.NewGuid();
        var correo = $"{alias}@example.test";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [identity].AspNetUsers
            (Id, Nombre, Email, NormalizedEmail, EmailConfirmed, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount)
            VALUES ({id}, {alias}, {correo}, {correo.ToUpperInvariant()}, 0, N'7654321012', 0, 0, 1, 0);
            """);
        return id;
    }

    private async Task<Guid> CrearPerfilAsync(string alias)
    {
        var usuario = await CrearUsuarioAsync(alias);
        var perfil = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO professionals.PerfilTecnico (Id, UsuarioId, Descripcion) VALUES ({perfil}, {usuario}, N'');
            """);
        return perfil;
    }

    private Task<byte[]> LeerVersionPerfilAsync(Guid tecnico) => db.Database.SqlQuery<byte[]>($"""
        SELECT [RowVersion] AS [Value] FROM professionals.PerfilTecnico WHERE Id = {tecnico}
        """).SingleAsync();

    private async Task<(int Especialidad, int Sector)> CrearCatalogoAsync()
    {
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO professionals.Especialidad (Codigo, Nombre, Activa) VALUES ('electricidad', N'Electricidad', 1);
            INSERT INTO professionals.Sector (Codigo, Nombre, Municipio, Activo) VALUES ('centro-prueba', N'Centro', N'Municipio de prueba', 1);
            """);
        var especialidad = await db.Database.SqlQueryRaw<int>("SELECT Id AS [Value] FROM professionals.Especialidad WHERE Codigo = 'electricidad'").SingleAsync();
        var sector = await db.Database.SqlQueryRaw<int>("SELECT Id AS [Value] FROM professionals.Sector WHERE Codigo = 'centro-prueba'").SingleAsync();
        return (especialidad, sector);
    }

    private async Task<Guid> CrearSolicitudAsync(Guid cliente, Guid tecnico, int especialidad, int sector, byte estado)
    {
        var id = Guid.NewGuid();
        var cita = new DateTimeOffset(2030, 6, 20, 9, 30, 0, TimeSpan.FromHours(-4));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO services.SolicitudServicio
            (Id, ClienteId, TecnicoId, EspecialidadId, SectorId, Titulo, Descripcion, Direccion, Estado, FechaPreferida, CreatedAtUtc)
            VALUES ({id}, {cliente}, {tecnico}, {especialidad}, {sector}, N'Revisar instalación',
                    N'La instalación necesita mantenimiento preventivo.', N'Dirección de prueba 001', {estado}, {cita}, SYSUTCDATETIME());
            """);
        return id;
    }

    private static FormattableString InsertarValoracion(Guid solicitud, Guid cliente, Guid tecnico, byte puntuacion) => $"""
        INSERT INTO reviews.Valoracion (Id, SolicitudId, ClienteId, TecnicoId, Puntuacion, CreatedAtUtc)
        VALUES (NEWID(), {solicitud}, {cliente}, {tecnico}, {puntuacion}, SYSUTCDATETIME());
        """;

    private static FormattableString InsertarHistorial(Guid solicitud, Guid actor) => $"""
        INSERT INTO services.HistorialSolicitud (SolicitudId, ActorId, TipoEvento, EstadoNuevo, CreatedAtUtc)
        VALUES ({solicitud}, {actor}, 1, 1, SYSUTCDATETIME());
        """;

    private static FormattableString InsertarIdempotencia(Guid solicitud, Guid cliente, Guid clave) => $"""
        INSERT INTO services.IdempotenciaSolicitud (ClienteId, Clave, SolicitudId, HashContenido)
        VALUES ({cliente}, {clave}, {solicitud}, HASHBYTES('SHA2_256', N'contenido de prueba'));
        """;

    private async Task RechazarAsync(FormattableString sql, params int[] numeros)
    {
        var error = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
        Assert.Contains(error.Number, numeros);
    }

    #endregion
}
