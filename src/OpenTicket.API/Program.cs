using System.Text.Json.Serialization;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using OpenTicket.API.Endpoints;
using OpenTicket.API.ErrorHandling;
using OpenTicket.API.Hosting;
using OpenTicket.API.OpenApi;
using OpenTicket.API.Security;
using OpenTicket.Application;
using OpenTicket.Application.Common;
using OpenTicket.Infrastructure;
using OpenTicket.Infrastructure.Demo;
using OpenTicket.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenTicketExternalLogins(builder.Configuration);
builder.Services.AddOpenTicketAuthorization();
builder.Services.AddOpenTicketRateLimiting();
builder.Services.AddProxySupport(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddValidation();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());

var app = builder.Build();

if (HostingExtensions.IsMigrateCommand(args))
{
    await app.MigrateDatabaseAsync();
    return;
}

if (app.Environment.IsDevelopment())
{
    // Development convenience only. Production applies migrations from CI or `--migrate` (docs/architecture.md D15).
    await app.MigrateDatabaseAsync();
}

await app.Services.SeedIdentityAsync();
await app.Services.SeedDemoAsync();

if (app.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    app.UseForwardedHeaders();
}

app.UseExceptionHandler();
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseSecurityHeaders();

// Static SPA files and development-only API docs run before authorization:
// the fallback "authenticated" policy would otherwise apply to them too.
app.UseStaticFiles();
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "OpenTicket API v1"));
    app.MapOpenApi().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Public by design: hosting platforms probe these without credentials. They return status only.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).AllowAnonymous();

var api = app.MapGroup("/api/v1");
api.MapAuthEndpoints();
api.MapTicketEndpoints();
api.MapSprintEndpoints();
api.MapCommentEndpoints();
api.MapCategoryEndpoints();
api.MapDashboardEndpoints();
api.MapUserEndpoints();

app.MapSpa();

await app.RunAsync();

/// <summary>Entry point; public so integration tests can use WebApplicationFactory.</summary>
public partial class Program;
