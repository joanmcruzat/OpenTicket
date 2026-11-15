using OpenTicket.Domain.Users;

namespace OpenTicket.Infrastructure.Demo;

internal sealed record DemoPerson(string Key, string DisplayName, UserRole Role)
{
    /// <summary>The reserved .invalid TLD guarantees mail to demo accounts can never be delivered.</summary>
    public string Email => $"{Key}@{DemoAccounts.EmailDomain}";
}

/// <summary>The cast of the demo. The first person of each role is the one visitors sign in as.</summary>
internal static class DemoAccounts
{
    public const string EmailDomain = "demo.openticket.invalid";

    public static readonly DemoPerson Admin = new("morgan", "Morgan Lee", UserRole.Admin);
    public static readonly DemoPerson Alex = new("alex", "Alex Rivera", UserRole.Agent);
    public static readonly DemoPerson Priya = new("priya", "Priya Shah", UserRole.Agent);
    public static readonly DemoPerson Sam = new("sam", "Sam Okafor", UserRole.Agent);
    public static readonly DemoPerson Jordan = new("jordan", "Jordan Kim", UserRole.Customer);
    public static readonly DemoPerson Taylor = new("taylor", "Taylor Brooks", UserRole.Customer);

    public static readonly IReadOnlyList<DemoPerson> All = [Admin, Alex, Priya, Sam, Jordan, Taylor];

    public static readonly IReadOnlyList<string> Categories = ["Frontend", "Backend", "Infrastructure", "Mobile"];

    public static DemoPerson SignInAs(UserRole role) => role switch
    {
        UserRole.Admin => Admin,
        UserRole.Agent => Alex,
        _ => Jordan,
    };

    public static bool IsDemoEmail(string? email) =>
        email is not null && email.EndsWith("@" + EmailDomain, StringComparison.OrdinalIgnoreCase);
}
