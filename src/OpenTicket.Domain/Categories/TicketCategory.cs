namespace OpenTicket.Domain.Categories;

/// <summary>Groups tickets by topic. Categories are deactivated, never deleted.</summary>
public sealed class TicketCategory
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private TicketCategory()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    public TicketCategory(string name, string? description, DateTime nowUtc)
    {
        Id = Guid.CreateVersion7();
        Name = string.Empty;
        NormalizedName = string.Empty;
        SetName(name);
        Description = NormalizeDescription(description);
        IsActive = true;
        CreatedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Upper-cased, trimmed name backing the case-insensitive unique index.</summary>
    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static string Normalize(string name) => name.Trim().ToUpperInvariant();

    public void Update(string name, string? description, DateTime nowUtc)
    {
        SetName(name);
        Description = NormalizeDescription(description);
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTime nowUtc) => SetActive(true, nowUtc);

    /// <summary>Inactive categories cannot be chosen for new tickets but stay on existing ones.</summary>
    public void Deactivate(DateTime nowUtc) => SetActive(false, nowUtc);

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, DescriptionMaxLength, nameof(description));
        return trimmed;
    }

    private void SetActive(bool isActive, DateTime nowUtc)
    {
        if (IsActive != isActive)
        {
            IsActive = isActive;
            UpdatedAtUtc = nowUtc;
        }
    }

    private void SetName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        ArgumentOutOfRangeException.ThrowIfGreaterThan(trimmed.Length, NameMaxLength, nameof(name));
        Name = trimmed;
        NormalizedName = Normalize(trimmed);
    }
}
