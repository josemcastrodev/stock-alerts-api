using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using StockAlerts.Core.Entities;

namespace StockAlerts.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no DateTimeOffset type and EF refuses to sort or compare by it there.
        // Storing it as a number keeps the same queries working on both providers.
        if (Database.IsSqlite())
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Store>(store =>
        {
            store.Property(s => s.Name).HasMaxLength(100);
            store.Property(s => s.Website).HasMaxLength(500);
        });

        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Name).HasMaxLength(200);
            product.Property(p => p.Category).HasMaxLength(100);
            product.HasIndex(p => p.Category);
        });

        modelBuilder.Entity<Listing>(listing =>
        {
            listing.Property(l => l.Url).HasMaxLength(2000);
            listing.Property(l => l.Price).HasPrecision(10, 2);

            // Concurrency token: if two instances of the worker see the same restock, the second UPDATE
            // matches no row (the status is no longer the one it read), its save fails and its alerts
            // are not inserted. This is what prevents duplicate alerts at the database level.
            listing.Property(l => l.Status).HasConversion<string>().HasMaxLength(16).IsConcurrencyToken();

            listing.HasOne(l => l.Store).WithMany().HasForeignKey(l => l.StoreId);
            listing.HasOne(l => l.Product).WithMany(p => p.Listings).HasForeignKey(l => l.ProductId);
            listing.HasIndex(l => new { l.StoreId, l.ProductId }).IsUnique();
        });

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Email).HasMaxLength(256);
            user.Property(u => u.Role).HasConversion<string>().HasMaxLength(16);
            user.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<Subscription>(subscription =>
        {
            subscription.HasKey(s => new { s.UserId, s.ProductId });
            subscription.HasOne<User>().WithMany().HasForeignKey(s => s.UserId);
            subscription.HasOne(s => s.Product).WithMany().HasForeignKey(s => s.ProductId);
            subscription.HasIndex(s => s.ProductId);
        });

        modelBuilder.Entity<Alert>(alert =>
        {
            alert.Property(a => a.Price).HasPrecision(10, 2);
            alert.HasOne<User>().WithMany().HasForeignKey(a => a.UserId);
            alert.HasOne(a => a.Listing).WithMany().HasForeignKey(a => a.ListingId);
            alert.HasIndex(a => new { a.UserId, a.CreatedAt });
        });
    }
}
