using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenTicket.Domain.Categories;

namespace OpenTicket.Infrastructure.Persistence.Configurations;

internal sealed class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.ToTable("ticket_category");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(TicketCategory.NameMaxLength).IsRequired();
        builder.Property(c => c.NormalizedName).HasMaxLength(TicketCategory.NameMaxLength).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(TicketCategory.DescriptionMaxLength);
        builder.Property(c => c.IsActive).HasDefaultValue(true);
        builder.HasIndex(c => c.NormalizedName).IsUnique();
    }
}
