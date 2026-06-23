using GharAagan.Models;
using GharAagan.Services;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Data;

public static class DbSeeder
{
    /// <summary>
    /// Applies migrations and seeds default categories plus a single admin account.
    /// Admin credentials come from configuration (Seed:AdminEmail / Seed:AdminPassword).
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, IConfiguration config)
    {
        await db.Database.MigrateAsync();

        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(
                new ServiceCategory { Name = "Plumbing", Description = "Pipes, leaks, fittings, fixtures." },
                new ServiceCategory { Name = "Electrical", Description = "Wiring, repairs, installations." },
                new ServiceCategory { Name = "Cleaning", Description = "Home and office cleaning." },
                new ServiceCategory { Name = "Painting", Description = "Interior and exterior painting." },
                new ServiceCategory { Name = "Carpentry", Description = "Furniture, doors, woodwork." },
                new ServiceCategory { Name = "Appliance Repair", Description = "AC, fridge, washing machine repair." });
            await db.SaveChangesAsync();
        }

        var adminEmail = config["Seed:AdminEmail"] ?? "admin@gharaagan.com";
        if (!await db.Users.AnyAsync(u => u.Email == adminEmail))
        {
            var adminPassword = config["Seed:AdminPassword"] ?? "Admin@123";
            var (hash, salt) = PasswordHasher.Hash(adminPassword);
            db.Users.Add(new User
            {
                FullName = "Site Admin",
                Email = adminEmail,
                PasswordHash = hash,
                PasswordSalt = salt,
                Role = UserRole.Admin
            });
            await db.SaveChangesAsync();
        }
    }
}
