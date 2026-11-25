namespace GoFix.Api;

internal static class SafeDiagnostics
{
    internal static void AddGoFixSafeDiagnostics(this ILoggingBuilder logging)
    {
        // Estas categorías pueden incluir URL sin filtrar, valores de binding o mensajes SQL.
        // GoFix.Api.Errors conserva estado, código, tipo y correlación sin esos contenidos.
        logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
        logging.AddFilter("Microsoft.AspNetCore.Http.RequestDelegateFactory", LogLevel.None);
        logging.AddFilter("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogLevel.None);
        logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.None);
    }
}
