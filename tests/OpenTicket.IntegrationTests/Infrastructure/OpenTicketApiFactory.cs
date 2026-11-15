using System.Security.Cryptography;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using OpenTicket.Infrastructure.Email;
using OpenTicket.Infrastructure.Persistence;

using Testcontainers.PostgreSql;

namespace OpenTicket.IntegrationTests.Infrastructure;

/// <summary>Hosts the API against a throwaway PostgreSQL container with all migrations applied.</summary>
public class OpenTicketApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@openticket.test";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public string AdminPassword { get; } = $"Admin-{Guid.NewGuid():N}";

    public string ConnectionString => _database.GetConnectionString();

    /// <summary>Every email the app "sent" during the test run.</summary>
    public CapturingEmailSender Emails { get; } = new();

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await _database.StartAsync();

        // Migrate before the host starts: the app seeds roles on startup and needs the schema.
        var options = new DbContextOptionsBuilder<OpenTicketDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new OpenTicketDbContext(options);
        await db.Database.MigrateAsync();
    }

    /// <summary>Hook for factories that need a differently configured app on their own database.</summary>
    protected virtual void ConfigureExtraSettings(IWebHostBuilder builder)
    {
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>A client that behaves like the SPA: HTTPS origin, cookies handled explicitly by the test.</summary>
    public HttpClient CreateApiClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" skips Development-only behaviour (user-secrets, auto-migrate) so tests never touch a local DB.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "100000");
        builder.UseSetting("App:PublicUrl", "https://openticket.test");
        builder.UseSetting("Email:LogOnly", "true");
        builder.UseSetting("Authentication:GitHub:ClientId", "test-client-id");
        builder.UseSetting("Authentication:GitHub:ClientSecret", "test-client-secret");
        builder.ConfigureTestServices(services => services.AddSingleton<IEmailSender>(Emails));
        ConfigureExtraSettings(builder);
    }
}

/// <summary>The app in public demo mode, on its own database so demo sprints and data never leak into other tests.</summary>
public sealed class DemoApiFactory : OpenTicketApiFactory
{
    protected override void ConfigureExtraSettings(IWebHostBuilder builder) => builder.UseSetting("Demo:Enabled", "true");
}

[CollectionDefinition(Name)]
public sealed class DemoTestGroup : ICollectionFixture<DemoApiFactory>
{
    public const string Name = "demo";
}

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<OpenTicketApiFactory>
{
    public const string Name = "api";
}
