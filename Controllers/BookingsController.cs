using GharAagan.Data;
using GharAagan.Dtos;
using GharAagan.Models;
using GharAagan.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BookingsController(AppDbContext db) => _db = db;

    // Customer creates a booking. A Pending payment is created automatically.
    [Authorize(Roles = "Customer")]
    [HttpPost]
    public async Task<ActionResult<BookingResponse>> Create(BookingRequest req)
    {
        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.Id == req.ServiceListingId);
        if (listing is null || !listing.IsActive)
            return BadRequest("Service is not available for booking.");

        // Normalize the client-supplied time to UTC so the future check is timezone-safe.
        var scheduledUtc = req.ScheduledAt.Kind == DateTimeKind.Utc
            ? req.ScheduledAt
            : req.ScheduledAt.ToUniversalTime();
        if (scheduledUtc < DateTime.UtcNow)
            return BadRequest("Scheduled time must be in the future.");

        var booking = new Booking
        {
            CustomerId = User.GetUserId(),
            ServiceListingId = listing.Id,
            ScheduledAt = scheduledUtc,
            Address = req.Address.Trim(),
            Notes = req.Notes,
            Status = BookingStatus.Pending,
            Payment = new Payment { Amount = listing.Price, Status = PaymentStatus.Pending }
        };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = booking.Id }, await LoadResponse(booking.Id));
    }

    // Customer: my bookings. Provider: bookings on my listings.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookingResponse>>> Mine()
    {
        var userId = User.GetUserId();
        var role = User.GetRole();

        var query = BaseQuery();
        query = role switch
        {
            "Provider" => query.Where(b => b.ServiceListing!.ProviderId == userId),
            "Admin" => query,
            _ => query.Where(b => b.CustomerId == userId)
        };

        var list = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();
        return Ok(list.Select(ToResponse));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingResponse>> Get(int id)
    {
        var booking = await BaseQuery().FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();
        if (!CanAccess(booking)) return Forbid();
        return Ok(ToResponse(booking));
    }

    // Provider acts on a booking for their own listing.
    [Authorize(Roles = "Provider")]
    [HttpPost("{id:int}/accept")]
    public Task<IActionResult> Accept(int id) => Transition(id, BookingStatus.Pending, BookingStatus.Accepted);

    [Authorize(Roles = "Provider")]
    [HttpPost("{id:int}/reject")]
    public Task<IActionResult> Reject(int id) => Transition(id, BookingStatus.Pending, BookingStatus.Rejected);

    [Authorize(Roles = "Provider")]
    [HttpPost("{id:int}/complete")]
    public Task<IActionResult> Complete(int id) => Transition(id, BookingStatus.Accepted, BookingStatus.Completed);

    // Customer cancels a booking that has not yet been completed/rejected.
    [Authorize(Roles = "Customer")]
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await _db.Bookings.Include(b => b.ServiceListing).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();
        if (booking.CustomerId != User.GetUserId()) return Forbid();
        if (booking.Status is BookingStatus.Completed or BookingStatus.Rejected or BookingStatus.Cancelled)
            return Conflict($"Cannot cancel a booking that is {booking.Status}.");

        booking.Status = BookingStatus.Cancelled;
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        { return Conflict("This booking changed in another request. Refresh and try again."); }
        return NoContent();
    }

    // Mock payment: customer pays for their booking.
    [Authorize(Roles = "Customer")]
    [HttpPost("{id:int}/pay")]
    public async Task<ActionResult<BookingResponse>> Pay(int id, PayRequest req)
    {
        var booking = await _db.Bookings.Include(b => b.Payment).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();
        if (booking.CustomerId != User.GetUserId()) return Forbid();
        // Payment is only valid once the provider has accepted (or already completed) the job.
        if (booking.Status is not (BookingStatus.Accepted or BookingStatus.Completed))
            return Conflict($"Payment is only allowed after the provider accepts the booking (currently {booking.Status}).");

        var payment = booking.Payment ?? new Payment { BookingId = booking.Id };
        if (payment.Status == PaymentStatus.Paid)
            return Conflict("This booking is already paid.");

        // Simulate a gateway success. Real integration (eSewa/Khalti/Stripe) would
        // verify a callback/signature here before marking the payment Paid.
        payment.Method = req.Method;
        payment.Status = PaymentStatus.Paid;
        payment.TransactionRef = "MOCK-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        payment.PaidAt = DateTime.UtcNow;
        booking.Payment = payment;

        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        { return Conflict("This booking changed in another request. Refresh and try again."); }
        return Ok(await LoadResponse(booking.Id));
    }

    // ---- helpers ----

    private IQueryable<Booking> BaseQuery() => _db.Bookings
        .Include(b => b.Customer)
        .Include(b => b.Payment)
        .Include(b => b.ServiceListing)!.ThenInclude(l => l!.Provider);

    private bool CanAccess(Booking b)
    {
        var userId = User.GetUserId();
        var role = User.GetRole();
        return role == "Admin"
            || b.CustomerId == userId
            || b.ServiceListing?.ProviderId == userId;
    }

    private async Task<IActionResult> Transition(int id, BookingStatus from, BookingStatus to)
    {
        var booking = await _db.Bookings.Include(b => b.ServiceListing).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();
        if (booking.ServiceListing?.ProviderId != User.GetUserId()) return Forbid();
        if (booking.Status != from)
            return Conflict($"Booking must be {from} to perform this action (currently {booking.Status}).");

        booking.Status = to;
        try { await _db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException)
        { return Conflict("This booking changed in another request. Refresh and try again."); }
        return NoContent();
    }

    private async Task<BookingResponse> LoadResponse(int id)
        => ToResponse(await BaseQuery().FirstAsync(b => b.Id == id));

    private static BookingResponse ToResponse(Booking b) => new()
    {
        Id = b.Id,
        ServiceListingId = b.ServiceListingId,
        ServiceTitle = b.ServiceListing?.Title ?? string.Empty,
        CustomerId = b.CustomerId,
        CustomerName = b.Customer?.FullName ?? string.Empty,
        ProviderId = b.ServiceListing?.ProviderId ?? 0,
        ProviderName = b.ServiceListing?.Provider?.FullName ?? string.Empty,
        ScheduledAt = b.ScheduledAt,
        Address = b.Address,
        Notes = b.Notes,
        Status = b.Status.ToString(),
        Amount = b.Payment?.Amount ?? 0,
        PaymentStatus = b.Payment?.Status.ToString() ?? PaymentStatus.Pending.ToString(),
        CreatedAt = b.CreatedAt
    };
}
