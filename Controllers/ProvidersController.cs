using GharAagan.Data;
using GharAagan.Models;
using GharAagan.Models.ViewModels;
using GharAagan.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

/// <summary>
/// Public browsing of providers: category listing (ranked by the
/// recommendation service) and provider detail pages with reviews.
/// </summary>
public class ProvidersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IRecommendationService _recommendations;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProvidersController(
        ApplicationDbContext context,
        IRecommendationService recommendations,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _recommendations = recommendations;
        _userManager = userManager;
    }

    /// <summary>Browse providers in a category, ranked; optional location filter.</summary>
    [HttpGet]
    public async Task<IActionResult> Browse(int categoryId, string? location)
    {
        var category = await _context.ServiceCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == categoryId);
        if (category is null) return NotFound();

        // Default the location filter to the signed-in customer's address so
        // nearby providers rank higher without extra typing.
        if (string.IsNullOrWhiteSpace(location) && User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            location = user?.Address;
        }

        var ranked = await _recommendations.GetRankedProvidersAsync(categoryId, location);

        return View(new BrowseProvidersViewModel
        {
            Category = category,
            LocationFilter = location,
            Providers = ranked
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var profile = await _context.ProviderProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.ServiceCategory)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (profile is null) return NotFound();

        var reviews = await _context.Reviews
            .AsNoTracking()
            .Include(r => r.Booking).ThenInclude(b => b.Customer)
            .Where(r => r.Booking.ProviderProfileId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var availability = await _context.ProviderAvailabilities
            .AsNoTracking()
            .Where(a => a.ProviderProfileId == id)
            .OrderBy(a => a.Day)
            .ToListAsync();

        return View(new ProviderDetailsViewModel
        {
            Profile = profile,
            Reviews = reviews,
            Availability = availability
        });
    }
}
