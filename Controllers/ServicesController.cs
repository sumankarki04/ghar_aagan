using System.Linq.Expressions;
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
        var query = _db.Listings.Where(l => l.IsActive);

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

        // Project in-query: rating average/count computed by SQL, no review rows loaded.
        var listings = await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(Projection)
            .ToListAsync();
        return Ok(listings);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ListingResponse>> Get(int id)
    {
        // Public endpoint: only active listings. Providers use /services/mine for their own (incl. inactive).
        var listing = await _db.Listings
            .Where(l => l.Id == id && l.IsActive)
            .Select(Projection)
            .FirstOrDefaultAsync();
        return listing is null ? NotFound() : Ok(listing);
    }

    // Provider: list my own listings (including inactive).
    [Authorize(Roles = "Provider")]
    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<ListingResponse>>> Mine()
    {
        var providerId = User.GetUserId();
        var listings = await _db.Listings
            .Where(l => l.ProviderId == providerId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(Projection)
            .ToListAsync();
        return Ok(listings);
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

        var dto = await _db.Listings.Where(l => l.Id == listing.Id).Select(Projection).FirstAsync();
        return CreatedAtAction(nameof(Get), new { id = listing.Id }, dto);
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

    // Single source of truth for shaping a listing. Translated to SQL by EF: the
    // average and count run as correlated aggregates, so review rows are never loaded.
    private static readonly Expression<Func<ServiceListing, ListingResponse>> Projection = l => new ListingResponse
    {
        Id = l.Id,
        Title = l.Title,
        Description = l.Description,
        Price = l.Price,
        City = l.City,
        IsActive = l.IsActive,
        CategoryId = l.CategoryId,
        CategoryName = l.Category!.Name,
        ProviderId = l.ProviderId,
        ProviderName = l.Provider!.FullName,
        ProviderVerified = l.Provider.IsVerified,
        AverageRating = l.Reviews.Average(r => (double?)r.Rating) ?? 0,
        ReviewCount = l.Reviews.Count,
        CreatedAt = l.CreatedAt
    };
}
