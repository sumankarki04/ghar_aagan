using GharAagan.Data;
using GharAagan.Models;
using GharAagan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

/// <summary>
/// Provider-side: dashboard, profile management, weekly availability,
/// and booking workflow (Accept / Reject / Mark Complete).
/// </summary>
[Authorize(Roles = "Provider")]
public class ProviderController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProviderController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private async Task<ProviderProfile?> GetMyProfileAsync(bool track = false)
    {
        var userId = _userManager.GetUserId(User)!;
        var query = _context.ProviderProfiles
            .Include(p => p.ServiceCategory)
            .Where(p => p.UserId == userId);
        if (!track) query = query.AsNoTracking();
        return await query.FirstOrDefaultAsync();
    }

    // ---------------- Dashboard ----------------

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var profile = await GetMyProfileAsync();
        if (profile is null) return RedirectToAction(nameof(EditProfile));

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.Customer)
            .Where(b => b.ProviderProfileId == profile.Id)
            .OrderByDescending(b => b.ScheduledDateTime)
            .ToListAsync();

        var completed = bookings.Where(b => b.Status == BookingStatus.Completed).ToList();

        return View(new ProviderDashboardViewModel
        {
            Profile = profile,
            PendingBookings = bookings.Where(b => b.Status == BookingStatus.Pending).ToList(),
            ActiveBookings = bookings.Where(b => b.Status is BookingStatus.Confirmed or BookingStatus.InProgress).ToList(),
            History = bookings.Where(b => b.Status is BookingStatus.Completed or BookingStatus.Cancelled or BookingStatus.Rejected).ToList(),
            CompletedJobs = completed.Count,
            TotalEarnings = completed.Sum(b => b.Price)
        });
    }

    // ---------------- Profile ----------------

    [HttpGet]
    public async Task<IActionResult> EditProfile()
    {
        var profile = await GetMyProfileAsync();
        await LoadCategoriesAsync();

        if (profile is null) return View(new ProviderProfileFormViewModel());

        return View(new ProviderProfileFormViewModel
        {
            Bio = profile.Bio,
            YearsExperience = profile.YearsExperience,
            HourlyRate = profile.HourlyRate,
            ServiceCategoryId = profile.ServiceCategoryId,
            Location = profile.Location
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(ProviderProfileFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return View(model);
        }

        var userId = _userManager.GetUserId(User)!;
        var profile = await _context.ProviderProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile is null)
        {
            profile = new ProviderProfile { UserId = userId };
            _context.ProviderProfiles.Add(profile);
            TempData["Success"] = "Profile created. An admin will review your account for verification.";
        }
        else
        {
            TempData["Success"] = "Profile updated.";
        }

        profile.Bio = model.Bio;
        profile.YearsExperience = model.YearsExperience;
        profile.HourlyRate = model.HourlyRate;
        profile.ServiceCategoryId = model.ServiceCategoryId;
        profile.Location = model.Location;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Dashboard));
    }

    // ---------------- Availability ----------------

    [HttpGet]
    public async Task<IActionResult> Availability()
    {
        var profile = await GetMyProfileAsync();
        if (profile is null) return RedirectToAction(nameof(EditProfile));

        var existing = await _context.ProviderAvailabilities
            .AsNoTracking()
            .Where(a => a.ProviderProfileId == profile.Id)
            .ToListAsync();

        var model = new AvailabilityFormViewModel();
        foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
        {
            var slot = existing.FirstOrDefault(a => a.Day == day);
            model.Slots.Add(new AvailabilitySlotInput
            {
                Day = day,
                Enabled = slot is not null,
                StartTime = slot?.StartTime ?? new TimeOnly(9, 0),
                EndTime = slot?.EndTime ?? new TimeOnly(18, 0)
            });
        }
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Availability(AvailabilityFormViewModel model)
    {
        var profile = await GetMyProfileAsync();
        if (profile is null) return RedirectToAction(nameof(EditProfile));

        foreach (var slot in model.Slots.Where(s => s.Enabled && s.EndTime <= s.StartTime))
            ModelState.AddModelError(string.Empty, $"{slot.Day}: end time must be after start time.");

        if (!ModelState.IsValid) return View(model);

        // Replace-all strategy: simple and safe for a weekly grid.
        var existing = _context.ProviderAvailabilities.Where(a => a.ProviderProfileId == profile.Id);
        _context.ProviderAvailabilities.RemoveRange(existing);

        foreach (var slot in model.Slots.Where(s => s.Enabled))
        {
            _context.ProviderAvailabilities.Add(new ProviderAvailability
            {
                ProviderProfileId = profile.Id,
                Day = slot.Day,
                StartTime = slot.StartTime,
                EndTime = slot.EndTime
            });
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "Availability updated.";
        return RedirectToAction(nameof(Dashboard));
    }

    // ---------------- Booking workflow ----------------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Accept(int id) =>
        TransitionAsync(id, from: new[] { BookingStatus.Pending }, to: BookingStatus.Confirmed, "Booking accepted.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Reject(int id) =>
        TransitionAsync(id, from: new[] { BookingStatus.Pending }, to: BookingStatus.Rejected, "Booking rejected.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Start(int id) =>
        TransitionAsync(id, from: new[] { BookingStatus.Confirmed }, to: BookingStatus.InProgress, "Job marked as in progress.");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Complete(int id) =>
        TransitionAsync(id, from: new[] { BookingStatus.Confirmed, BookingStatus.InProgress }, to: BookingStatus.Completed, "Job marked as completed.");

    /// <summary>Shared, ownership-checked status transition for provider actions.</summary>
    private async Task<IActionResult> TransitionAsync(int bookingId, BookingStatus[] from, BookingStatus to, string successMessage)
    {
        var userId = _userManager.GetUserId(User)!;
        var booking = await _context.Bookings
            .Include(b => b.ProviderProfile)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.ProviderProfile.UserId == userId);
        if (booking is null) return NotFound();

        if (!from.Contains(booking.Status))
        {
            TempData["Error"] = $"Cannot change a booking that is {booking.Status}.";
            return RedirectToAction(nameof(Dashboard));
        }

        booking.Status = to;
        await _context.SaveChangesAsync();

        TempData["Success"] = successMessage;
        return RedirectToAction(nameof(Dashboard));
    }

    private async Task LoadCategoriesAsync()
    {
        ViewBag.Categories = new SelectList(
            await _context.ServiceCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name");
    }
}
