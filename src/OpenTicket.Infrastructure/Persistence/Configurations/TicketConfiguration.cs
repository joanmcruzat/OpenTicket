using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("ticket", t =>
        {
            t.HasCheckConstraint("ck_ticket_status", "status BETWEEN 0 AND 5");
            t.HasCheckConstraint("ck_ticket_type", "type BETWEEN 0 AND 3");
            t.HasCheckConstraint("ck_ticket_priority", "priority BETWEEN 0 AND 3");
            // Done (4) and Won't Do (5) are the closed statuses; only they carry a closed timestamp.
            t.HasCheckConstraint("ck_ticket_closed_at", "(status >= 4) = (closed_at_utc IS NOT NULL)");
            t.HasCheckConstraint(
                "ck_ticket_wont_do_note",
                "status <> 5 OR (closure_note IS NOT NULL AND length(trim(closure_note)) > 0)");
        });

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Number).UseIdentityByDefaultColumn();
        builder.Property(t => t.Number).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        builder.HasIndex(t => t.Number).IsUnique();

        builder.Property(t => t.Title).HasMaxLength(Ticket.TitleMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(Ticket.DescriptionMaxLength).IsRequired();
        builder.Property(t => t.ClosureNote).HasMaxLength(Ticket.ClosureNoteMaxLength);
        builder.Property(t => t.Version).IsRowVersion();

        builder.HasOne<TicketCategory>().WithMany()
            .HasForeignKey(t => t.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(t => t.CreatedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(t => t.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Sprint>().WithMany()
            .HasForeignKey(t => t.SprintId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.History).WithOne()
            .HasForeignKey(h => h.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(t => t.History).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Each index serves a documented query (docs/architecture.md §3.3).
        builder.HasIndex(t => new { t.CreatedById, t.CreatedAtUtc }).IsDescending(false, true);
        builder.HasIndex(t => new { t.AssignedToId, t.Status });
        builder.HasIndex(t => new { t.Status, t.CreatedAtUtc }).IsDescending(false, true);
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => new { t.SprintId, t.Status });
    }
}

internal sealed class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.ToTable("ticket_comment");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Body).HasMaxLength(TicketComment.BodyMaxLength).IsRequired();

        builder.HasOne<Ticket>().WithMany()
            .HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.TicketId, c.CreatedAtUtc });
    }
}

internal sealed class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.ToTable("ticket_history", t =>
            t.HasCheckConstraint("ck_ticket_history_change_type", "change_type BETWEEN 0 AND 8"));
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();
        builder.Property(h => h.OldValue).HasMaxLength(TicketHistory.ValueMaxLength);
        builder.Property(h => h.NewValue).HasMaxLength(TicketHistory.ValueMaxLength);

        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(h => h.ChangedById).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.TicketId, h.CreatedAtUtc });
    }
}
