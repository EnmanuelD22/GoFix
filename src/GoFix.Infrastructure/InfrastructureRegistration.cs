using GoFix.Application.Modules.Services;
using GoFix.Infrastructure.Modules.Services;
using GoFix.Application.Modules.Identity;
using GoFix.Application.Modules.Professionals;
using GoFix.Application.Modules.Reviews;
using GoFix.Infrastructure.Modules.Reviews;
using GoFix.Infrastructure.Modules.Professionals;
using GoFix.Infrastructure.Modules.Identity;
using GoFix.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        services.TryAddSingleton(TimeProvider.System);
        services.AddIdentityCore<GoFixUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<GoFixDbContext>()
            .AddSignInManager();
        services.AddScoped<IIdentityStore, EfIdentityStore>();
        services.AddScoped<IdentityService>();
        services.AddScoped<IProfessionalStore, EfProfessionalStore>();
        services.AddScoped<ProfessionalService>();
        services.AddScoped<IProfessionalDirectory, EfProfessionalDirectory>();
        services.AddScoped<DirectoryService>();
        services.AddScoped<IReviewQuery, EfReviewQuery>();
        services.AddScoped<IReviewEligibility, EfReviewEligibility>();
        services.AddScoped<IReviewStore, EfReviewStore>();
        services.AddScoped<ReviewService>();
        services.AddScoped<IRepositorioSolicitudes, EfServiceRequestStore>();
        services.AddScoped<ServiceRequestService>();

        services.AddHealthChecks().AddCheck<SqlServerHealthCheck>(
            "sql-server",
            tags: [ReadinessTag],
            timeout: TimeSpan.FromSeconds(5));

        return services;
    }
}
