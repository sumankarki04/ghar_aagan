using GharAagan.Data;
using GharAagan.Dtos;
using GharAagan.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    public AdminController(AppDbContext db) => _db = db;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        // SQLite cannot SUM decimal server-side, so pull paid amounts and sum in memory.
        var paidAmounts = await _db.Payments
            .Where(p => p.Status == PaymentStatus.Paid)
            .Select(p => p.Amount)
            .ToListAsync();

        var stats = new
        {
            customers = await _db.Users.CountAsync(u => u.Role == UserRole.Customer),
            providers = await _db.Users.CountAsync(u => u.Role == UserRole.Provider),
            listings = await _db.Listings.CountAsync(),
            bookings = await _db.Bookings.CountAsync(),
            completed = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Completed),
            paidRevenue = paidAmounts.Sum()
        };
        return Ok(stats);
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users([FromQuery] UserRole? role)
    {
        var query = _db.Users.AsQueryable();
        if (role is UserRole r) query = query.Where(u => u.Role == r);

        var users = await query
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.Phone,
                Role = u.Role.ToString(),
                u.IsVerified,
                KycStatus = u.KycStatus.ToString(),
                u.CreatedAt
            })
            .ToListAsync();
        return Ok(users);
    }

    [HttpGet("listings")]
    public async Task<IActionResult> Listings()
    {
        var listings = await _db.Listings
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new
            {
                l.Id,
                l.Title,
                Provider = l.Provider!.FullName,
                l.City,
                l.Price,
                l.IsActive,
                l.CreatedAt
            })
            .ToListAsync();
        return Ok(listings);
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> Bookings()
    {
        // Pull enums as values, stringify in memory (EF can't translate enum.ToString()).
        var rows = await _db.Bookings
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id,
                Service = b.ServiceListing!.Title,
                Customer = b.Customer!.FullName,
                Provider = b.ServiceListing!.Provider!.FullName,
                b.Status,
                Amount = b.Payment != null ? b.Payment.Amount : 0,
                PayStatus = b.Payment != null ? b.Payment.Status : PaymentStatus.Pending,
                b.ScheduledAt
            })
            .ToListAsync();

        return Ok(rows.Select(r => new
        {
            r.Id, r.Service, r.Customer, r.Provider,
            Status = r.Status.ToString(),
            r.Amount,
            Payment = r.PayStatus.ToString(),
            r.ScheduledAt
        }));
    }

    // KYC review queue. Defaults to providers awaiting review (Pending).
    [HttpGet("kyc")]
    public async Task<ActionResult<IEnumerable<KycReviewItem>>> KycQueue([FromQuery] KycStatus? status)
    {
        var want = status ?? KycStatus.Pending;
        var providers = await _db.Users
            .Include(u => u.KycDocuments)
            .Where(u => u.Role == UserRole.Provider && u.KycStatus == want)
            .OrderBy(u => u.KycSubmittedAt)
            .ToListAsync();

        return Ok(providers.Select(u => new KycReviewItem
        {
            ProviderId = u.Id,
            ProviderName = u.FullName,
            Email = u.Email,
            Status = u.KycStatus.ToString(),
            IsVerified = u.IsVerified,
            SubmittedAt = u.KycSubmittedAt,
            RejectionReason = u.KycRejectionReason,
            Documents = u.KycDocuments.OrderBy(d => d.DocType).Select(d => new KycDocItem
            {
                Id = d.Id,
                DocType = d.DocType.ToString(),
                FileName = d.FileName,
                Url = $"/api/kyc/file/{d.Id}",
                UploadedAt = d.UploadedAt
            }).ToList()
        }));
    }

    // One provider's KYC detail (for the review modal).
    [HttpGet("kyc/{providerId:int}")]
    public async Task<ActionResult<KycReviewItem>> KycOne(int providerId)
    {
        var u = await _db.Users.Include(x => x.KycDocuments)
            .FirstOrDefaultAsync(x => x.Id == providerId && x.Role == UserRole.Provider);
        if (u is null) return NotFound();
        return Ok(new KycReviewItem
        {
            ProviderId = u.Id,
            ProviderName = u.FullName,
            Email = u.Email,
            Status = u.KycStatus.ToString(),
            IsVerified = u.IsVerified,
            SubmittedAt = u.KycSubmittedAt,
            RejectionReason = u.KycRejectionReason,
            Documents = u.KycDocuments.OrderBy(d => d.DocType).Select(d => new KycDocItem
            {
                Id = d.Id,
                DocType = d.DocType.ToString(),
                FileName = d.FileName,
                Url = $"/api/kyc/file/{d.Id}",
                UploadedAt = d.UploadedAt
            }).ToList()
        });
    }

    // Approve KYC → provider becomes verified.
    [HttpPost("kyc/{providerId:int}/approve")]
    public async Task<IActionResult> ApproveKyc(int providerId)
    {
        var user = await _db.Users.FindAsync(providerId);
        if (user is null || user.Role != UserRole.Provider) return NotFound();
        if (user.KycStatus != KycStatus.Pending)
            return Conflict($"KYC is {user.KycStatus}, not Pending.");

        user.KycStatus = KycStatus.Approved;
        user.IsVerified = true;
        user.KycReviewedAt = DateTime.UtcNow;
        user.KycRejectionReason = null;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // Reject KYC (requires a reason) → provider stays unverified, may resubmit.
    [HttpPost("kyc/{providerId:int}/reject")]
    public async Task<IActionResult> RejectKyc(int providerId, KycRejectRequest req)
    {
        var user = await _db.Users.FindAsync(providerId);
        if (user is null || user.Role != UserRole.Provider) return NotFound();
        if (user.KycStatus != KycStatus.Pending)
            return Conflict($"KYC is {user.KycStatus}, not Pending.");

        user.KycStatus = KycStatus.Rejected;
        user.IsVerified = false;
        user.KycReviewedAt = DateTime.UtcNow;
        user.KycRejectionReason = req.Reason.Trim();
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
