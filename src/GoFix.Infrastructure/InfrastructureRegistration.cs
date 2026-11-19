using GoFix.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GoFix.Infrastructure;

public static class InfrastructureRegistration
{
    public const string ReadinessTag = "sql";

    /// <summary>Registra el contexto por petición y permite iniciar el host aunque SQL no esté disponible.</summary>
    public static IServiceCollection AddGoFixInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<GoFixDbContext>(options => options.UseSqlServer(
            configuration.GetConnectionString("GoFix"),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory")));

        services.AddHealthChecks().AddCheck<SqlServerHealthCheck>(
            "sql-server",
            tags: [ReadinessTag],
            timeout: TimeSpan.FromSeconds(5));

        return services;
    }
}
