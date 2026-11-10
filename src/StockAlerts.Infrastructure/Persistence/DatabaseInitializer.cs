using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StockAlerts.Core.Entities;

namespace StockAlerts.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = scope.ServiceProvider.GetRequiredService<IOptions<SeedOptions>>().Value;

        // Migrations target SQL Server, the real database. SQLite is only here so the project runs
        // without installing anything, so its schema is created straight from the model.
        if (db.Database.IsSqlServer())
        {
            await db.Database.MigrateAsync();
        }
        else
        {
            await db.Database.EnsureCreatedAsync();
        }

        if (seed is { AdminEmail: not null, AdminPassword: not null })
        {
            await EnsureAdminAsync(scope.ServiceProvider, db, seed.AdminEmail, seed.AdminPassword);
        }

        if (seed.DemoData && !await db.Stores.AnyAsync())
        {
            AddDemoCatalog(db);
        }

        await db.SaveChangesAsync();
    }

    private static async Task EnsureAdminAsync(IServiceProvider services, AppDbContext db, string email, string password)
    {
        var normalizedEmail = User.NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            return;
        }

        var hasher = services.GetRequiredService<IPasswordHasher<User>>();
        var clock = services.GetRequiredService<TimeProvider>();

        var admin = new User(normalizedEmail, UserRole.Admin, clock.GetUtcNow());
        admin.SetPasswordHash(hasher.HashPassword(admin, password));
        db.Users.Add(admin);
    }

    private static void AddDemoCatalog(AppDbContext db)
    {
        var dragonsDen = new Store("Dragon's Den", "https://dragons-den.example");
        var manaVault = new Store("Mana Vault", "https://mana-vault.example");

        var boosterBox = new Product("Ember Skies Booster Box", "Booster boxes");
        var trainerBox = new Product("Ember Skies Elite Trainer Box", "Trainer boxes");
        var bundle = new Product("Tidal Realms Booster Bundle", "Bundles");

        db.AddRange(dragonsDen, manaVault, boosterBox, trainerBox, bundle);
        db.AddRange(
            new Listing(dragonsDen.Id, boosterBox.Id, "https://dragons-den.example/p/ember-skies-booster-box"),
            new Listing(manaVault.Id, boosterBox.Id, "https://mana-vault.example/products/ember-skies-booster-box"),
            new Listing(dragonsDen.Id, trainerBox.Id, "https://dragons-den.example/p/ember-skies-elite-trainer-box"),
            new Listing(manaVault.Id, bundle.Id, "https://mana-vault.example/products/tidal-realms-booster-bundle"));
    }
}
