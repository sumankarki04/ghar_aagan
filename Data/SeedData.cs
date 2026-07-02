using GharAagan.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Data;

/// <summary>
/// Seeds the database on first run so the app is demo-ready immediately:
///  - Roles: Admin, Provider, Customer
///  - 1 Admin account, 1 demo Customer, 5 verified sample Providers
///  - 6 service categories
///  - Completed bookings with reviews so ratings/recommendations show real data
/// Idempotent: safe to run on every startup.
/// </summary>
public static class SeedData
{
    public const string AdminEmail = "admin@gharaagan.com";
    public const string CustomerEmail = "sita@example.com";
    public const string DefaultPassword = "Demo@123";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Apply pending migrations (creates the SQLite file on first run).
        await context.Database.MigrateAsync();

        // ---- Roles ----
        foreach (var role in new[] { "Admin", "Provider", "Customer" })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ---- Service categories ----
        if (!await context.ServiceCategories.AnyAsync())
        {
            context.ServiceCategories.AddRange(
                new ServiceCategory { Name = "Plumbing", Description = "Pipe repairs, leakages, fittings and bathroom installations.", IconClass = "bi-droplet-half" },
                new ServiceCategory { Name = "Electrical", Description = "Wiring, MCB/fuse issues, fan and light installation.", IconClass = "bi-lightning-charge" },
                new ServiceCategory { Name = "Cleaning", Description = "Deep home cleaning, kitchen, bathroom and sofa cleaning.", IconClass = "bi-stars" },
                new ServiceCategory { Name = "Carpentry", Description = "Furniture repair, doors, windows and custom woodwork.", IconClass = "bi-hammer" },
                new ServiceCategory { Name = "Painting", Description = "Interior and exterior painting, putty and polishing.", IconClass = "bi-palette" },
                new ServiceCategory { Name = "Appliance Repair", Description = "Washing machine, fridge, TV and appliance servicing.", IconClass = "bi-gear-wide-connected" });
            await context.SaveChangesAsync();
        }

        // ---- Admin ----
        var admin = await EnsureUserAsync(userManager, AdminEmail, "Ghar Aagan Admin", "9800000000", "Kathmandu", "Admin");

        // ---- Demo customer ----
        var customer = await EnsureUserAsync(userManager, CustomerEmail, "Sita Sharma", "9811111111", "Baneshwor, Kathmandu", "Customer");

        // ---- Sample providers ----
        if (!await context.ProviderProfiles.AnyAsync())
        {
            var categories = await context.ServiceCategories.ToDictionaryAsync(c => c.Name);

            var providerSpecs = new[]
            {
                new { Email = "ram.plumber@example.com",     Name = "Ram Bahadur Thapa", Phone = "9841000001", Cat = "Plumbing",        Rate = 600m, Years = 8,  Loc = "Kathmandu - Baneshwor",  Bio = "Licensed plumber with 8 years of experience in residential piping, leak repair and bathroom fitting across Kathmandu valley." },
                new { Email = "hari.electric@example.com",   Name = "Hari Prasad Gurung", Phone = "9841000002", Cat = "Electrical",     Rate = 700m, Years = 10, Loc = "Kathmandu - Koteshwor",  Bio = "Certified electrician. House wiring, inverter setup, and emergency electrical repair. Safety-first workmanship." },
                new { Email = "maya.cleaning@example.com",   Name = "Maya Tamang",        Phone = "9841000003", Cat = "Cleaning",       Rate = 450m, Years = 5,  Loc = "Lalitpur - Pulchowk",    Bio = "Professional deep-cleaning specialist. Kitchens, bathrooms, sofas and full-home cleaning with eco-friendly products." },
                new { Email = "krishna.wood@example.com",    Name = "Krishna Shrestha",   Phone = "9841000004", Cat = "Carpentry",      Rate = 650m, Years = 12, Loc = "Bhaktapur - Suryabinayak", Bio = "Third-generation carpenter. Custom furniture, door/window repair and modular kitchen woodwork." },
                new { Email = "gopal.paint@example.com",     Name = "Gopal Magar",        Phone = "9841000005", Cat = "Painting",       Rate = 500m, Years = 7,  Loc = "Kathmandu - Kalanki",    Bio = "Interior and exterior painting expert. Clean finishing, putty work, and colour consultation included." },
            };

            var rnd = new Random(42);
            foreach (var spec in providerSpecs)
            {
                var user = await EnsureUserAsync(userManager, spec.Email, spec.Name, spec.Phone, spec.Loc, "Provider");

                var profile = new ProviderProfile
                {
                    UserId = user.Id,
                    Bio = spec.Bio,
                    YearsExperience = spec.Years,
                    HourlyRate = spec.Rate,
                    ServiceCategoryId = categories[spec.Cat].Id,
                    Location = spec.Loc,
                    IsVerified = true
                };
                context.ProviderProfiles.Add(profile);
                await context.SaveChangesAsync();

                // Weekly availability: Sun-Fri 09:00-18:00 (Nepali work week).
                foreach (var day in new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
                {
                    context.ProviderAvailabilities.Add(new ProviderAvailability
                    {
                        ProviderProfileId = profile.Id,
                        Day = day,
                        StartTime = new TimeOnly(9, 0),
                        EndTime = new TimeOnly(18, 0)
                    });
                }

                // 2-3 completed bookings with reviews per provider → demo-ready ratings.
                int reviewCount = rnd.Next(2, 4);
                var comments = new[]
                {
                    "Very professional and finished on time. Highly recommended!",
                    "Good work, fair price. Will book again.",
                    "Arrived on schedule and solved the problem quickly.",
                    "Polite and skilled. Left the workspace clean."
                };
                for (int i = 0; i < reviewCount; i++)
                {
                    var scheduled = DateTime.UtcNow.AddDays(-(rnd.Next(7, 60)));
                    var booking = new Booking
                    {
                        CustomerId = customer.Id,
                        ProviderProfileId = profile.Id,
                        ScheduledDateTime = scheduled,
                        Address = "Baneshwor, Kathmandu",
                        Status = BookingStatus.Completed,
                        Notes = "Seeded demo booking.",
                        Price = spec.Rate * rnd.Next(1, 4),
                        CreatedAt = scheduled.AddDays(-2)
                    };
                    context.Bookings.Add(booking);
                    await context.SaveChangesAsync();

                    context.Reviews.Add(new Review
                    {
                        BookingId = booking.Id,
                        Rating = rnd.Next(4, 6), // 4 or 5 stars for seeded data
                        Comment = comments[rnd.Next(comments.Length)],
                        CreatedAt = scheduled.AddDays(1)
                    });
                    await context.SaveChangesAsync();
                }

                // Recompute denormalized average rating.
                profile.AverageRating = await context.Reviews
                    .Where(r => r.Booking.ProviderProfileId == profile.Id)
                    .AverageAsync(r => (double)r.Rating);
                await context.SaveChangesAsync();
            }
        }

        // ---- Demo chat thread (so Messages is not empty on first login) ----
        if (!await context.Messages.AnyAsync())
        {
            var demoBooking = await context.Bookings
                .Include(b => b.ProviderProfile)
                .OrderBy(b => b.Id)
                .FirstOrDefaultAsync();
            if (demoBooking is not null)
            {
                var providerUserId = demoBooking.ProviderProfile.UserId;
                var baseTime = demoBooking.CreatedAt;
                context.Messages.AddRange(
                    new Message { BookingId = demoBooking.Id, SenderId = demoBooking.CustomerId, Content = "Namaste! The kitchen tap has been leaking since yesterday. Can you bring spare washers?", SentAt = baseTime.AddMinutes(10), IsRead = true },
                    new Message { BookingId = demoBooking.Id, SenderId = providerUserId, Content = "Namaste! Yes, I always carry washers and basic fittings. Is the leak from the base or the spout?", SentAt = baseTime.AddMinutes(25), IsRead = true },
                    new Message { BookingId = demoBooking.Id, SenderId = demoBooking.CustomerId, Content = "From the base. There is also a little water under the sink.", SentAt = baseTime.AddMinutes(32), IsRead = true },
                    new Message { BookingId = demoBooking.Id, SenderId = providerUserId, Content = "Understood — likely the seal. I will check the pipe joint too. See you at the scheduled time!", SentAt = baseTime.AddMinutes(40), IsRead = false });
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email, string fullName, string phone, string address, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                PhoneNumber = phone,
                Address = address
            };
            var result = await userManager.CreateAsync(user, DefaultPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Seed user '{email}' failed: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);

        return user;
    }
}
