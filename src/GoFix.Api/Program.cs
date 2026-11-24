using GoFix.Api;
using GoFix.Api.Modules.Identity;
using GoFix.Api.Modules.Professionals;
using GoFix.Api.Modules.Services;
using GoFix.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGoFixInfrastructure(builder.Configuration);
builder.Services.AddGoFixHttp();
builder.Services.AddIdentityHttp();
builder.Services.AddOpenApi();

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("GoFix");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

var app = builder.Build();

app.UseTrustedClientAddresses();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseGoFixErrors();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains(InfrastructureRegistration.ReadinessTag)
});
var api = app.MapGroup("/api/v1");
api.MapGoFixApi();
api.MapIdentityEndpoints();
api.MapProfessionalEndpoints();
api.MapServiceRequestEndpoints();

app.Run();

public partial class Program;
