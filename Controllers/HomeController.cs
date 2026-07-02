using System.Diagnostics;
using GharAagan.Data;
using GharAagan.Models;
using GharAagan.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var reviewedCount = await _context.Reviews.CountAsync();

        var model = new HomeViewModel
        {
            Categories = await _context.ServiceCategories
                .AsNoTracking()
                .Include(c => c.Providers)
                .OrderBy(c => c.Name)
                .ToListAsync(),

            // Top-rated verified providers for the "Featured" rail.
            FeaturedProviders = await _context.ProviderProfiles
                .AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.ServiceCategory)
                .Where(p => p.IsVerified)
                .OrderByDescending(p => p.AverageRating)
                .ThenByDescending(p => p.YearsExperience)
                .Take(3)
                .ToListAsync(),

            VerifiedProviderCount = await _context.ProviderProfiles.CountAsync(p => p.IsVerified),
            CompletedJobCount = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Completed),
            AverageRating = reviewedCount == 0
                ? 0
                : await _context.Reviews.AverageAsync(r => (double)r.Rating)
        };

        return View(model);
    }

    public IActionResult Privacy() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
