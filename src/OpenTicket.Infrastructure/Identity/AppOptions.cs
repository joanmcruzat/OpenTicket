namespace OpenTicket.Infrastructure.Identity;

public sealed class AppOptions
{
    public const string Section = "App";

    /// <summary>
    /// The address users open, e.g. https://tickets.example.com. Used to build links in emails. It comes from
    /// configuration, never from the request's Host header, so a forged Host cannot poison reset links.
    /// Empty disables features that need it (password reset by email).
    /// </summary>
    public string? PublicUrl { get; init; }

    public bool HasValidPublicUrl =>
        Uri.TryCreate(PublicUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
