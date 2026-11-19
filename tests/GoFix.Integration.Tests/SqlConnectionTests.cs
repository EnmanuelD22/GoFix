using GoFix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace GoFix.Integration.Tests;
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOFIX_TEST_SQLSERVER")))
            Skip = "Configura GOFIX_TEST_SQLSERVER con una base exclusiva de pruebas.";
    }
}
public sealed class SqlConnectionTests
{
    // Solo abre una conexion: no crea, migra ni elimina bases de datos.
    [SqlServerFact]
    public async Task ConfiguredTestDatabaseIsReachable()
    {
        var options = new DbContextOptionsBuilder<GoFixDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("GOFIX_TEST_SQLSERVER")).Options;
        await using var context = new GoFixDbContext(options);
        Assert.True(await context.Database.CanConnectAsync());
    }
}
