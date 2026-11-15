using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenTicket.Domain.Sprints;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.Infrastructure.Persistence.Configurations;

internal sealed class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("sprint", t =>
        {
            t.HasCheckConstraint("ck_sprint_state", "state BETWEEN 0 AND 2");
            // Planned sprints may have no dates yet; active and completed ones always do.
            t.HasCheckConstraint("ck_sprint_dates", "state = 0 OR (start_date IS NOT NULL AND end_date IS NOT NULL)");
            t.HasCheckConstraint("ck_sprint_date_order", "end_date IS NULL OR start_date IS NULL OR end_date >= start_date");
            t.HasCheckConstraint("ck_sprint_completed_at", "(state = 2) = (completed_at_utc IS NOT NULL)");
        });

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Name).HasMaxLength(Sprint.NameMaxLength).IsRequired();
        builder.Property(s => s.Goal).HasMaxLength(Sprint.GoalMaxLength);

        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(s => s.CreatedById).OnDelete(DeleteBehavior.Restrict);

        // At most one active sprint: a partial unique index on the active state.
        builder.HasIndex(s => s.State).IsUnique().HasFilter("state = 1").HasDatabaseName("ux_sprint_single_active");
        builder.HasIndex(s => new { s.State, s.CreatedAtUtc });
    }
}
