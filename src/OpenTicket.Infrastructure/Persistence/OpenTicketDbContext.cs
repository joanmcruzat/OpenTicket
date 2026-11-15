using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

using OpenTicket.Application.Common;

using OpenTicket.Domain.Categories;
using OpenTicket.Domain.Sprints;
using OpenTicket.Domain.Tickets;
using OpenTicket.Infrastructure.Identity;

namespace OpenTicket.Infrastructure.Persistence;

public sealed class OpenTicketDbContext(DbContextOptions<OpenTicketDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IOpenTicketDbContext, IDataProtectionKeyContext
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    public DbSet<TicketHistory> TicketHistory => Set<TicketHistory>();

    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(OpenTicketDbContext).Assembly);
        builder.Entity<DataProtectionKey>().ToTable("data_protection_key");
    }
}
