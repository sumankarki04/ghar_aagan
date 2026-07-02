using GharAagan.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Data;

/// <summary>
/// EF Core context. Provider-agnostic: the database provider is chosen in
/// Program.cs (UseSqlite by default). Switching to SQL Server is a one-line
/// change there — swap UseSqlite(...) for UseSqlServer(...) and update the
/// connection string in appsettings.json. No changes needed in this file.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<ProviderProfile> ProviderProfiles => Set<ProviderProfile>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ProviderAvailability> ProviderAvailabilities => Set<ProviderAvailability>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // One provider profile per user.
        builder.Entity<ProviderProfile>()
            .HasIndex(p => p.UserId)
            .IsUnique();

        builder.Entity<ProviderProfile>()
            .HasOne(p => p.User)
            .WithOne(u => u.ProviderProfile!)
            .HasForeignKey<ProviderProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // One review per booking.
        builder.Entity<Review>()
            .HasIndex(r => r.BookingId)
            .IsUnique();

        builder.Entity<Booking>()
            .HasOne(b => b.Customer)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Booking>()
            .HasOne(b => b.ProviderProfile)
            .WithMany(p => p.Bookings)
            .HasForeignKey(b => b.ProviderProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ProviderAvailability>()
            .HasOne(a => a.ProviderProfile)
            .WithMany(p => p.Availabilities)
            .HasForeignKey(a => a.ProviderProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Message>()
            .HasOne(m => m.Booking)
            .WithMany()
            .HasForeignKey(m => m.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Fast unread-count + thread queries.
        builder.Entity<Message>()
            .HasIndex(m => new { m.BookingId, m.SentAt });
    }
}
