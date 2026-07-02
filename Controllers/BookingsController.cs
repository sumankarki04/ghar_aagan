using GharAagan.Data;
using GharAagan.Models;
using GharAagan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

/// <summary>
/// Customer-side booking flow: create, list, cancel, review.
/// </summary>
[Authorize(Roles = "Customer")]
public class BookingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public BookingsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int providerId)
    {
        var profile = await _context.ProviderProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ServiceCategory)
            .FirstOrDefaultAsync(p => p.Id == providerId);
        if (profile is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);

        return View(new BookingCreateViewModel
        {
            ProviderProfileId = profile.Id,
            ProviderName = profile.User.FullName,
            CategoryName = profile.ServiceCategory.Name,
            HourlyRate = profile.HourlyRate,
            Address = user?.Address ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(BookingCreateViewModel model)
    {
        var profile = await _context.ProviderProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ServiceCategory)
            .FirstOrDefaultAsync(p => p.Id == model.ProviderProfileId);
        if (profile is null) return NotFound();

        if (model.ScheduledDateTime <= DateTime.Now)
            ModelState.AddModelError(nameof(model.ScheduledDateTime), "Please pick a future date and time.");

        if (!ModelState.IsValid)
        {
            // Re-populate display-only fields the form does not post back.
            model.ProviderName = profile.User.FullName;
            model.CategoryName = profile.ServiceCategory.Name;
            model.HourlyRate = profile.HourlyRate;
            return View(model);
        }

        var userId = _userManager.GetUserId(User)!;

        var booking = new Booking
        {
            CustomerId = userId,
            ProviderProfileId = profile.Id,
            ScheduledDateTime = model.ScheduledDateTime,
            Address = model.Address,
            Notes = model.Notes,
            Status = BookingStatus.Pending,
            // Price estimate = provider hourly rate × estimated hours (snapshot).
            Price = profile.HourlyRate * model.EstimatedHours
        };

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        TempData["Success"] = $"Booking request sent to {profile.User.FullName}. You'll see updates in My Bookings.";
        return RedirectToAction(nameof(MyBookings));
    }

    [HttpGet]
    public async Task<IActionResult> MyBookings()
    {
        var userId = _userManager.GetUserId(User)!;

        var bookings = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.ServiceCategory)
            .Include(b => b.Review)
            .Where(b => b.CustomerId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return View(bookings);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var booking = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.ServiceCategory)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == userId);
        if (booking is null) return NotFound();

        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var booking = await _context.Bookings
            .FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == userId);
        if (booking is null) return NotFound();

        // Customers may only cancel bookings that haven't been started.
        if (booking.Status != BookingStatus.Pending && booking.Status != BookingStatus.Confirmed)
        {
            TempData["Error"] = "Only pending or confirmed bookings can be cancelled.";
            return RedirectToAction(nameof(MyBookings));
        }

        booking.Status = BookingStatus.Cancelled;
        await _context.SaveChangesAsync();

        TempData["Success"] = "Booking cancelled.";
        return RedirectToAction(nameof(MyBookings));
    }

    [HttpGet]
    public async Task<IActionResult> Review(int id)
    {
        var userId = _userManager.GetUserId(User)!;
        var booking = await _context.Bookings
            .AsNoTracking()
            .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
            .Include(b => b.Review)
            .FirstOrDefaultAsync(b => b.Id == id && b.CustomerId == userId);
        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.Completed)
        {
            TempData["Error"] = "You can only review completed bookings.";
            return RedirectToAction(nameof(MyBookings));
        }
        if (booking.Review is not null)
        {
            TempData["Error"] = "This booking has already been reviewed.";
            return RedirectToAction(nameof(MyBookings));
        }

        return View(new ReviewCreateViewModel
        {
            BookingId = booking.Id,
            ProviderName = booking.ProviderProfile.User.FullName
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(ReviewCreateViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;
        var booking = await _context.Bookings
            .Include(b => b.Review)
            .Include(b => b.ProviderProfile)
            .FirstOrDefaultAsync(b => b.Id == model.BookingId && b.CustomerId == userId);
        if (booking is null) return NotFound();

        if (booking.Status != BookingStatus.Completed || booking.Review is not null)
        {
            TempData["Error"] = "This booking cannot be reviewed.";
            return RedirectToAction(nameof(MyBookings));
        }

        if (!ModelState.IsValid) return View(model);

        _context.Reviews.Add(new Review
        {
            BookingId = booking.Id,
            Rating = model.Rating,
            Comment = model.Comment
        });
        await _context.SaveChangesAsync();

        // Keep the provider's denormalized average in sync.
        booking.ProviderProfile.AverageRating = await _context.Reviews
            .Where(r => r.Booking.ProviderProfileId == booking.ProviderProfileId)
            .AverageAsync(r => (double)r.Rating);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Thank you! Your review has been posted.";
        return RedirectToAction(nameof(MyBookings));
    }
}
