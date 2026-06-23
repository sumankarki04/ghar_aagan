using GharAagan.Models;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ServiceCategory> Categories => Set<ServiceCategory>();
    public DbSet<ServiceListing> Listings => Set<ServiceListing>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<KycDocument> KycDocuments => Set<KycDocument>();

    // Re-stamp concurrency tokens before persisting. For Modified entities EF keeps the
    // loaded value in the WHERE clause, so a competing writer that already changed the row
    // triggers DbUpdateConcurrencyException instead of a silent overwrite.
    public override int SaveChanges()
    {
        StampConcurrencyTokens();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampConcurrencyTokens();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<IConcurrencyStamped>())
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.RowVersion = Guid.NewGuid();
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Optimistic-concurrency tokens (SQLite has no native rowversion; see Stamp()).
        b.Entity<Booking>().Property(x => x.RowVersion).IsConcurrencyToken();
        b.Entity<Payment>().Property(x => x.RowVersion).IsConcurrencyToken();

        // KYC documents belong to a provider; removing the user removes their docs.
        b.Entity<KycDocument>()
            .HasOne(d => d.Provider)
            .WithMany(u => u.KycDocuments)
            .HasForeignKey(d => d.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);

        // SQLite has no native decimal; store as TEXT with conversion handled by provider.
        b.Entity<ServiceListing>()
            .Property(s => s.Price)
            .HasColumnType("decimal(18,2)");

        b.Entity<Payment>()
            .Property(p => p.Amount)
            .HasColumnType("decimal(18,2)");

        // One payment per booking
        b.Entity<Payment>()
            .HasOne(p => p.Booking)
            .WithOne(bk => bk.Payment)
            .HasForeignKey<Payment>(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // One review per booking
        b.Entity<Review>()
            .HasOne(r => r.Booking)
            .WithOne(bk => bk.Review)
            .HasForeignKey<Review>(r => r.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Review>()
            .HasIndex(r => r.BookingId)
            .IsUnique();

        // Listing -> Provider (no cascade to avoid multiple cascade paths)
        b.Entity<ServiceListing>()
            .HasOne(s => s.Provider)
            .WithMany(u => u.Listings)
            .HasForeignKey(s => s.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Booking -> Customer (restrict)
        b.Entity<Booking>()
            .HasOne(bk => bk.Customer)
            .WithMany(u => u.Bookings)
            .HasForeignKey(bk => bk.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Booking -> Listing (restrict)
        b.Entity<Booking>()
            .HasOne(bk => bk.ServiceListing)
            .WithMany(s => s.Bookings)
            .HasForeignKey(bk => bk.ServiceListingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
