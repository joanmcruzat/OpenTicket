using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

using OpenTicket.Application.Auth;
using OpenTicket.Domain.Common;
using OpenTicket.Domain.Users;
using OpenTicket.Infrastructure.Persistence;

namespace OpenTicket.Infrastructure.Identity;

internal sealed partial class ExternalLoginService(
    UserManager<ApplicationUser> users,
    OpenTicketDbContext db,
    AuthService auth,
    TimeProvider clock,
    ILogger<ExternalLoginService> logger) : IExternalLoginService
{
    public async Task<Result<AuthSession>> SignInAsync(ExternalIdentity identity, CancellationToken ct)
    {
        var user = await users.FindByLoginAsync(identity.Provider, identity.ProviderKey);
        if (user is null)
        {
            // Matching by email is safe only because the provider verified that the person controls the mailbox.
            if (string.IsNullOrWhiteSpace(identity.Email) || !identity.EmailVerified)
            {
                return AuthErrors.ExternalEmailUnverified;
            }

            var linked = await LinkOrCreateAsync(identity, identity.Email.Trim(), ct);
            if (!linked.IsSuccess)
            {
                return linked.Error!;
            }

            user = linked.Value!;
        }

        if (!user.IsActive)
        {
            return AuthErrors.AccountDisabled;
        }

        return await auth.StartSessionAsync(user, ct);
    }

    private async Task<Result<ApplicationUser>> LinkOrCreateAsync(ExternalIdentity identity, string email, CancellationToken ct)
    {
        var login = new UserLoginInfo(identity.Provider, identity.ProviderKey, identity.Provider);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var user = await users.FindByEmailAsync(email);
        var isNew = user is null;
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = DisplayNameFor(identity, email),
                CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            };
            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
            {
                return IdentityErrorMapper.ToError(created);
            }

            EnsureSucceeded(await users.AddToRoleAsync(user, nameof(UserRole.Customer)), "assign the Customer role");
        }

        EnsureSucceeded(await users.AddLoginAsync(user, login), "link the external login");
        await transaction.CommitAsync(ct);
        LogLinked(identity.Provider, user.Id, isNew);
        return user;
    }

    private static string DisplayNameFor(ExternalIdentity identity, string email)
    {
        var name = string.IsNullOrWhiteSpace(identity.DisplayName) ? email.Split('@')[0] : identity.DisplayName.Trim();
        return name.Length > ApplicationUser.DisplayNameMaxLength ? name[..ApplicationUser.DisplayNameMaxLength] : name;
    }

    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Could not {action}: {result}");
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{Provider} login linked to user {UserId} (new account: {IsNew})")]
    private partial void LogLinked(string provider, Guid userId, bool isNew);
}
