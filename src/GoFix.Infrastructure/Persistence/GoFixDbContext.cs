using Microsoft.EntityFrameworkCore;

namespace GoFix.Infrastructure.Persistence;

/// <summary>Contexto relacional compartido por los módulos del monolito.</summary>
public sealed class GoFixDbContext(DbContextOptions<GoFixDbContext> options) : DbContext(options);
