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
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReviewsController(AppDbContext db) => _db = db;

    // Public: all reviews for a listing.
    [HttpGet("listing/{listingId:int}")]
    public async Task<ActionResult<IEnumerable<ReviewResponse>>> ForListing(int listingId)
    {
        var reviews = await _db.Reviews
            .Include(r => r.Customer)
            .Where(r => r.ServiceListingId == listingId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        return Ok(reviews.Select(ToResponse));
    }

    // Customer reviews their own completed booking. One review per booking.
    [Authorize(Roles = "Customer")]
    [HttpPost]
    public async Task<ActionResult<ReviewResponse>> Create(ReviewRequest req)
    {
        var booking = await _db.Bookings.Include(b => b.Payment).FirstOrDefaultAsync(b => b.Id == req.BookingId);
        if (booking is null) return NotFound("Booking not found.");
        if (booking.CustomerId != User.GetUserId()) return Forbid();
        if (booking.Status != BookingStatus.Completed)
            return Conflict("You can only review a completed booking.");
        if (booking.Payment?.Status != PaymentStatus.Paid)
            return Conflict("Please pay for the booking before leaving a review.");
        if (await _db.Reviews.AnyAsync(r => r.BookingId == booking.Id))
            return Conflict("You have already reviewed this booking.");

        var review = new Review
        {
            BookingId = booking.Id,
            CustomerId = booking.CustomerId,
            ServiceListingId = booking.ServiceListingId,
            Rating = req.Rating,
            Comment = req.Comment
        };
        _db.Reviews.Add(review);
        await _db.SaveChangesAsync();

        await _db.Entry(review).Reference(r => r.Customer).LoadAsync();
        return Ok(ToResponse(review));
    }

    private static ReviewResponse ToResponse(Review r) => new()
    {
        Id = r.Id,
        ServiceListingId = r.ServiceListingId,
        CustomerId = r.CustomerId,
        CustomerName = r.Customer?.FullName ?? string.Empty,
        Rating = r.Rating,
        Comment = r.Comment,
        CreatedAt = r.CreatedAt
    };
}
