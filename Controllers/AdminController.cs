using GharAagan.Data;
using GharAagan.Models;
using GharAagan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

/// <summary>
/// Admin control panel: platform stats, provider verification & management,
/// user suspension, booking oversight, category CRUD, review moderation.
/// </summary>
[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // ---------------- Dashboard ----------------

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var customers = await _userManager.GetUsersInRoleAsync("Customer");
        var providers = await _userManager.GetUsersInRoleAsync("Provider");
        var reviewCount = await _context.Reviews.CountAsync();

        var statusCounts = await _context.Bookings
            .GroupBy(b => b.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var categoryStats = await _context.ServiceCategories
            .AsNoTracking()
            .Select(c => new CategoryStat
            {
                Name = c.Name,
                IconClass = c.IconClass,
                ProviderCount = c.Providers.Count,
                BookingCount = _context.Bookings.Count(b => b.ProviderProfile.ServiceCategoryId == c.Id)
            })
            .OrderByDescending(c => c.BookingCount)
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalUsers = await _context.Users.CountAsync(),
            TotalCustomers = customers.Count,
            TotalProviders = providers.Count,
            TotalBookings = await _context.Bookings.CountAsync(),
            CompletedBookings = statusCounts.GetValueOrDefault(BookingStatus.Completed),
            PendingVerifications = await _context.ProviderProfiles.CountAsync(p => !p.IsVerified),
            // SQLite cannot SUM decimals server-side — aggregate as double, convert back.
            TotalRevenue = (decimal)await _context.Bookings
                .Where(b => b.Status == BookingStatus.Completed)
                .SumAsync(b => (double)b.Price),
            TotalReviews = reviewCount,
            PlatformAverageRating = reviewCount == 0 ? 0 : await _context.Reviews.AverageAsync(r => (double)r.Rating),
            StatusCounts = statusCounts,
            CategoryStats = categoryStats,
            RecentBookings = await _context.Bookings
                .AsNoTracking()
                .Include(b => b.Customer)
                .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
                .Include(b => b.ProviderProfile).ThenInclude(p => p.ServiceCategory)
                .OrderByDescending(b => b.CreatedAt)
                .Take(6)
                .ToListAsync()
        };
        return View(model);
    }

    // ---------------- Providers (verification + management) ----------------

    [HttpGet]
    public async Task<IActionResult> Providers(string filter = "all")
    {
        var query = _context.ProviderProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ServiceCategory)
            .AsQueryable();

        query = filter switch
        {
            "pending" => query.Where(p => !p.IsVerified),
            "verified" => query.Where(p => p.IsVerified),
            _ => query
        };

        ViewBag.Filter = filter;
        ViewBag.PendingCount = await _context.ProviderProfiles.CountAsync(p => !p.IsVerified);
        return View(await query.OrderBy(p => p.IsVerified).ThenBy(p => p.Id).ToListAsync());
    }

    // Kept for the old verification-queue link; redirects to unified page.
    [HttpGet]
    public IActionResult Verifications() => RedirectToAction(nameof(Providers), new { filter = "pending" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleVerify(int id)
    {
        var profile = await _context.ProviderProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == id);
        if (profile is null) return NotFound();

        profile.IsVerified = !profile.IsVerified;
        await _context.SaveChangesAsync();

        TempData["Success"] = profile.IsVerified
            ? $"{profile.User.FullName} is now verified."
            : $"Verification revoked for {profile.User.FullName}.";
        return RedirectToAction(nameof(Providers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var profile = await _context.ProviderProfiles
            .Include(p => p.Bookings)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (profile is null) return NotFound();

        if (profile.Bookings.Any())
        {
            TempData["Error"] = "Cannot reject a provider that already has bookings. Revoke verification instead.";
            return RedirectToAction(nameof(Providers));
        }

        _context.ProviderProfiles.Remove(profile);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Provider profile rejected and removed.";
        return RedirectToAction(nameof(Providers));
    }

    // ---------------- Bookings oversight ----------------

    [HttpGet]
    public async Task<IActionResult> Bookings(string? status)
    {
        var query = _context.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.ServiceCategory)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<BookingStatus>(status, out var parsed))
            query = query.Where(b => b.Status == parsed);

        ViewBag.Status = status;
        return View(await query.OrderByDescending(b => b.CreatedAt).ToListAsync());
    }

    /// <summary>Full booking drill-down: parties, timeline, review, message log (dispute oversight).</summary>
    [HttpGet]
    public async Task<IActionResult> Booking(int id)
    {
        var booking = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.ServiceCategory)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        ViewBag.Messages = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.BookingId == id)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        return View(booking);
    }

    /// <summary>User drill-down: account info, roles, provider profile, booking history.</summary>
    [HttpGet]
    public async Task<IActionResult> User(string id)
    {
        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        ViewBag.Roles = string.Join(", ", await _userManager.GetRolesAsync(user));
        ViewBag.IsSuspended = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow;

        ViewBag.ProviderProfile = await _context.ProviderProfiles
            .AsNoTracking()
            .Include(p => p.ServiceCategory)
            .FirstOrDefaultAsync(p => p.UserId == id);

        // Bookings where the user participates (as customer or as provider).
        ViewBag.Bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.ServiceCategory)
            .Where(b => b.CustomerId == id || b.ProviderProfile.UserId == id)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(user);
    }

    /// <summary>Admin override: force-cancel a problematic booking (dispute resolution).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking is null) return NotFound();

        if (booking.Status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.Rejected)
        {
            TempData["Error"] = $"Cannot cancel a booking that is already {booking.Status}.";
            return RedirectToAction(nameof(Bookings));
        }

        booking.Status = BookingStatus.Cancelled;
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Booking #{booking.Id} cancelled by admin.";
        return RedirectToAction(nameof(Bookings));
    }

    // ---------------- Users (search + suspend) ----------------

    [HttpGet]
    public async Task<IActionResult> Users(string? q)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(term) ||
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(term)));
        }

        var users = await query.OrderBy(u => u.FullName).ToListAsync();
        var bookingCounts = await _context.Bookings
            .GroupBy(b => b.CustomerId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var rows = new List<AdminUserRow>();
        foreach (var user in users)
        {
            rows.Add(new AdminUserRow
            {
                User = user,
                Roles = string.Join(", ", await _userManager.GetRolesAsync(user)),
                IsSuspended = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow,
                BookingCount = bookingCounts.GetValueOrDefault(user.Id)
            });
        }

        ViewBag.Query = q;
        return View(rows);
    }

    /// <summary>Suspend / reactivate an account via Identity lockout. Admins cannot be suspended.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSuspend(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, "Admin"))
        {
            TempData["Error"] = "Admin accounts cannot be suspended.";
            return RedirectToAction(nameof(Users));
        }

        bool suspended = user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow;
        await _userManager.SetLockoutEnabledAsync(user, true);
        await _userManager.SetLockoutEndDateAsync(user, suspended ? null : DateTimeOffset.MaxValue);
        // Invalidate existing sessions when suspending.
        if (!suspended) await _userManager.UpdateSecurityStampAsync(user);

        TempData["Success"] = suspended
            ? $"{user.FullName} reactivated."
            : $"{user.FullName} suspended.";
        return RedirectToAction(nameof(Users));
    }

    // ---------------- Categories CRUD ----------------

    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var categories = await _context.ServiceCategories
            .AsNoTracking()
            .Include(c => c.Providers)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(categories);
    }

    [HttpGet]
    public IActionResult CreateCategory() => View("CategoryForm", new CategoryFormViewModel());

    [HttpGet]
    public async Task<IActionResult> EditCategory(int id)
    {
        var category = await _context.ServiceCategories.FindAsync(id);
        if (category is null) return NotFound();

        return View("CategoryForm", new CategoryFormViewModel
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IconClass = category.IconClass
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCategory(CategoryFormViewModel model)
    {
        if (!ModelState.IsValid) return View("CategoryForm", model);

        // Enforce unique names (case-insensitive).
        bool duplicate = await _context.ServiceCategories
            .AnyAsync(c => c.Id != model.Id && c.Name.ToLower() == model.Name.Trim().ToLower());
        if (duplicate)
        {
            ModelState.AddModelError(nameof(model.Name), "A category with this name already exists.");
            return View("CategoryForm", model);
        }

        if (model.Id == 0)
        {
            _context.ServiceCategories.Add(new ServiceCategory
            {
                Name = model.Name.Trim(),
                Description = model.Description?.Trim(),
                IconClass = model.IconClass.Trim()
            });
            TempData["Success"] = "Category created.";
        }
        else
        {
            var category = await _context.ServiceCategories.FindAsync(model.Id);
            if (category is null) return NotFound();
            category.Name = model.Name.Trim();
            category.Description = model.Description?.Trim();
            category.IconClass = model.IconClass.Trim();
            TempData["Success"] = "Category updated.";
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.ServiceCategories
            .Include(c => c.Providers)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (category is null) return NotFound();

        if (category.Providers.Any())
        {
            TempData["Error"] = $"Cannot delete '{category.Name}' — {category.Providers.Count} provider(s) are registered under it.";
            return RedirectToAction(nameof(Categories));
        }

        _context.ServiceCategories.Remove(category);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Category deleted.";
        return RedirectToAction(nameof(Categories));
    }

    // ---------------- Review moderation ----------------

    [HttpGet]
    public async Task<IActionResult> Reviews()
    {
        var reviews = await _context.Reviews
            .AsNoTracking()
            .Include(r => r.Booking).ThenInclude(b => b.Customer)
            .Include(r => r.Booking).ThenInclude(b => b.ProviderProfile).ThenInclude(p => p.User)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return View(reviews);
    }

    /// <summary>Remove an abusive/fake review and recompute the provider's average.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReview(int id)
    {
        var review = await _context.Reviews
            .Include(r => r.Booking)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (review is null) return NotFound();

        int providerProfileId = review.Booking.ProviderProfileId;
        _context.Reviews.Remove(review);
        await _context.SaveChangesAsync();

        // Recompute the provider's denormalized average.
        var profile = await _context.ProviderProfiles.FindAsync(providerProfileId);
        if (profile is not null)
        {
            var ratings = await _context.Reviews
                .Where(r => r.Booking.ProviderProfileId == providerProfileId)
                .Select(r => (double)r.Rating)
                .ToListAsync();
            profile.AverageRating = ratings.Count == 0 ? 0 : ratings.Average();
            await _context.SaveChangesAsync();
        }

        TempData["Success"] = "Review removed and rating recalculated.";
        return RedirectToAction(nameof(Reviews));
    }
}
