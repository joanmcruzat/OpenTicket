using System.Security.Claims;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using OpenTicket.Application.Auth;
using OpenTicket.Application.Users;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.Infrastructure.Identity;

internal static class IdentityRegistration
{
    public static IServiceCollection AddOpenTicketIdentity(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.Section)
            .ValidateDataAnnotations()
            .Validate(o => o.HasStrongSigningKey(), "Jwt:SigningKey must be a base64 string of at least 32 bytes.")
            .ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration(SeedOptions.Section);
        services.AddOptions<AppOptions>()
            .BindConfiguration(AppOptions.Section)
            .Validate(o => string.IsNullOrWhiteSpace(o.PublicUrl) || o.HasValidPublicUrl,
                "App:PublicUrl must be an absolute http(s) URL, e.g. https://tickets.example.com.")
            .ValidateOnStart();

        // Keys for reset tokens and OAuth state live in the database, so they survive restarts (Render sleeps when idle).
        services.AddDataProtection()
            .SetApplicationName("OpenTicket")
            .PersistKeysToDbContext<OpenTicketDbContext>();
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(1));

        services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<OpenTicketDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<TokenService>();
        services.AddScoped<AuthService>();
        services.AddScoped<IAuthService>(sp => sp.GetRequiredService<AuthService>());
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddScoped<IExternalLoginService, ExternalLoginService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Value.Issuer,
                    ValidAudience = jwt.Value.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.Value.GetSigningKeyBytes()),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = OpenTicketClaims.Name,
                    RoleClaimType = OpenTicketClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
                bearer.Events = new JwtBearerEvents { OnTokenValidated = RejectStaleUsersAsync };
            });

        return services;
    }

    /// <summary>
    /// A token stays valid for up to 15 minutes. Re-check on every request (one indexed lookup) that the user
    /// is still active and still has the role in the token, so deactivation and demotion take effect at once.
    /// </summary>
    private static async Task RejectStaleUsersAsync(TokenValidatedContext context)
    {
        var subject = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var role = context.Principal?.FindFirstValue(OpenTicketClaims.Role);
        if (!Guid.TryParse(subject, out var userId) || role is null)
        {
            context.Fail("Token is missing required claims.");
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<OpenTicketDbContext>();
        var isCurrent = await (
                from user in db.Users
                where user.Id == userId && user.IsActive
                join userRole in db.UserRoles on user.Id equals userRole.UserId
                join dbRole in db.Roles on userRole.RoleId equals dbRole.Id
                where dbRole.Name == role
                select user.Id)
            .AnyAsync(context.HttpContext.RequestAborted);
        if (!isCurrent)
        {
            context.Fail("The user is inactive or their role has changed.");
        }
    }
}
