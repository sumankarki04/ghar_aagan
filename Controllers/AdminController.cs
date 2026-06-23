using GharAagan.Data;
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
                u.CreatedAt
            })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost("providers/{id:int}/verify")]
    public Task<IActionResult> Verify(int id) => SetVerified(id, true);

    [HttpPost("providers/{id:int}/unverify")]
    public Task<IActionResult> Unverify(int id) => SetVerified(id, false);

    private async Task<IActionResult> SetVerified(int id, bool value)
    {
        var user = await _db.Users.FindAsync(id);
        if (user is null) return NotFound();
        if (user.Role != UserRole.Provider)
            return BadRequest("Only providers can be verified.");
        user.IsVerified = value;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
