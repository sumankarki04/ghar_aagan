using GharAagan.Data;
using GharAagan.Dtos;
using GharAagan.Models;
using GharAagan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ServicesController(AppDbContext db) => _db = db;

    // Public search: filter by keyword, category, city, price range. Active listings only.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ListingResponse>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] int? categoryId,
        [FromQuery] string? city,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice)
    {
        var query = _db.Listings
            .Include(l => l.Category)
            .Include(l => l.Provider)
            .Include(l => l.Reviews)
            .Where(l => l.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // Case-insensitive: SQLite's Contains/instr is case-sensitive, so compare lowercased.
            var k = keyword.Trim().ToLower();
            query = query.Where(l => l.Title.ToLower().Contains(k)
                || (l.Description != null && l.Description.ToLower().Contains(k)));
        }
        if (categoryId is int cid) query = query.Where(l => l.CategoryId == cid);
        if (!string.IsNullOrWhiteSpace(city)) query = query.Where(l => l.City == city.Trim());
        if (minPrice is decimal min) query = query.Where(l => l.Price >= min);
        if (maxPrice is decimal max) query = query.Where(l => l.Price <= max);

        var listings = await query.OrderByDescending(l => l.CreatedAt).ToListAsync();
        return Ok(listings.Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ListingResponse>> Get(int id)
    {
        var listing = await _db.Listings
            .Include(l => l.Category)
            .Include(l => l.Provider)
            .Include(l => l.Reviews)
            .FirstOrDefaultAsync(l => l.Id == id);
        return listing is null ? NotFound() : Ok(ToResponse(listing));
    }

    // Provider: list my own listings (including inactive).
    [Authorize(Roles = "Provider")]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ListingResponse>>> Mine()
    {
        var providerId = User.GetUserId();
        var listings = await _db.Listings
            .Include(l => l.Category)
            .Include(l => l.Provider)
            .Include(l => l.Reviews)
            .Where(l => l.ProviderId == providerId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
        return Ok(listings.Select(ToResponse));
    }

    [Authorize(Roles = "Provider")]
    [HttpPost]
    public async Task<ActionResult<ListingResponse>> Create(ListingRequest req)
    {
        if (!await _db.Categories.AnyAsync(c => c.Id == req.CategoryId))
            return BadRequest("Invalid category.");

        var listing = new ServiceListing
        {
            Title = req.Title.Trim(),
            Description = req.Description,
            Price = req.Price,
            City = req.City.Trim(),
            CategoryId = req.CategoryId,
            IsActive = req.IsActive,
            ProviderId = User.GetUserId()
        };
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();

        // Reload with navs for response shaping.
        await _db.Entry(listing).Reference(l => l.Category).LoadAsync();
        await _db.Entry(listing).Reference(l => l.Provider).LoadAsync();
        return CreatedAtAction(nameof(Get), new { id = listing.Id }, ToResponse(listing));
    }

    [Authorize(Roles = "Provider")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ListingRequest req)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();
        if (listing.ProviderId != User.GetUserId()) return Forbid();
        if (!await _db.Categories.AnyAsync(c => c.Id == req.CategoryId))
            return BadRequest("Invalid category.");

        listing.Title = req.Title.Trim();
        listing.Description = req.Description;
        listing.Price = req.Price;
        listing.City = req.City.Trim();
        listing.CategoryId = req.CategoryId;
        listing.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Provider deletes own listing; Admin can delete any.
    [Authorize(Roles = "Provider,Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();
        if (User.GetRole() != "Admin" && listing.ProviderId != User.GetUserId())
            return Forbid();
        if (await _db.Bookings.AnyAsync(b => b.ServiceListingId == id))
            return Conflict("Cannot delete a listing that has bookings. Deactivate it instead.");

        _db.Listings.Remove(listing);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static ListingResponse ToResponse(ServiceListing l) => new()
    {
        Id = l.Id,
        Title = l.Title,
        Description = l.Description,
        Price = l.Price,
        City = l.City,
        IsActive = l.IsActive,
        CategoryId = l.CategoryId,
        CategoryName = l.Category?.Name ?? string.Empty,
        ProviderId = l.ProviderId,
        ProviderName = l.Provider?.FullName ?? string.Empty,
        AverageRating = l.Reviews.Count > 0 ? Math.Round(l.Reviews.Average(r => r.Rating), 2) : 0,
        ReviewCount = l.Reviews.Count,
        CreatedAt = l.CreatedAt
    };
}
