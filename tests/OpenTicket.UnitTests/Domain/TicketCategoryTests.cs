using OpenTicket.Domain.Categories;

namespace OpenTicket.UnitTests.Domain;

public sealed class TicketCategoryTests
{
    private static readonly DateTime Now = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_TrimsNameAndNormalizesForUniqueness()
    {
        var category = new TicketCategory("  Billing ", null, Now);

        Assert.Equal("Billing", category.Name);
        Assert.Equal("BILLING", category.NormalizedName);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Normalize_DifferentCasing_ProducesSameValue() =>
        Assert.Equal(TicketCategory.Normalize("billing"), TicketCategory.Normalize(" BILLING "));

    [Fact]
    public void Constructor_NameTooLong_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TicketCategory(new string('x', TicketCategory.NameMaxLength + 1), null, Now));
}
