using GharAagan.Data;
using GharAagan.Models;
using GharAagan.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GharAagan.Controllers;

/// <summary>
/// Per-booking chat between the booking's customer and provider
/// (pattern: job-scoped messaging, as on Urban Company / TaskRabbit).
/// Inbox lists conversations; Thread shows one booking's messages.
/// </summary>
[Authorize(Roles = "Customer,Provider")]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MessagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>Bookings the current user participates in (as customer or provider).</summary>
    private IQueryable<Booking> MyBookings(string userId) =>
        _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.User)
            .Include(b => b.ProviderProfile).ThenInclude(p => p.ServiceCategory)
            .Where(b => b.CustomerId == userId || b.ProviderProfile.UserId == userId);

    [HttpGet]
    public async Task<IActionResult> Inbox()
    {
        var userId = _userManager.GetUserId(User)!;

        var bookings = await MyBookings(userId)
            .AsNoTracking()
            // Chat is only meaningful once a real job relationship exists.
            .Where(b => b.Status != BookingStatus.Rejected && b.Status != BookingStatus.Cancelled)
            .ToListAsync();

        var bookingIds = bookings.Select(b => b.Id).ToList();
        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => bookingIds.Contains(m.BookingId))
            .ToListAsync();

        var conversations = bookings
            .Select(b =>
            {
                var thread = messages.Where(m => m.BookingId == b.Id).OrderBy(m => m.SentAt).ToList();
                var isCustomer = b.CustomerId == userId;
                return new ConversationSummary
                {
                    BookingId = b.Id,
                    OtherPartyName = isCustomer ? b.ProviderProfile.User.FullName : b.Customer.FullName,
                    ServiceName = b.ProviderProfile.ServiceCategory.Name,
                    BookingStatus = b.Status,
                    ScheduledDateTime = b.ScheduledDateTime,
                    LastMessage = thread.LastOrDefault()?.Content,
                    LastMessageAt = thread.LastOrDefault()?.SentAt,
                    UnreadCount = thread.Count(m => m.SenderId != userId && !m.IsRead)
                };
            })
            .OrderByDescending(c => c.LastMessageAt ?? DateTime.MinValue)
            .ThenByDescending(c => c.ScheduledDateTime)
            .ToList();

        return View(conversations);
    }

    [HttpGet]
    public async Task<IActionResult> Thread(int id)
    {
        var userId = _userManager.GetUserId(User)!;

        var booking = await MyBookings(userId).FirstOrDefaultAsync(b => b.Id == id);
        if (booking is null) return NotFound();

        // Mark the other party's messages as read.
        var unread = await _context.Messages
            .Where(m => m.BookingId == id && m.SenderId != userId && !m.IsRead)
            .ToListAsync();
        foreach (var message in unread) message.IsRead = true;
        if (unread.Count > 0) await _context.SaveChangesAsync();

        var thread = await _context.Messages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.BookingId == id)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var isCustomer = booking.CustomerId == userId;
        return View(new MessageThreadViewModel
        {
            Booking = booking,
            OtherPartyName = isCustomer ? booking.ProviderProfile.User.FullName : booking.Customer.FullName,
            OtherPartyPhone = (isCustomer ? booking.ProviderProfile.User.PhoneNumber : booking.Customer.PhoneNumber) ?? "",
            CurrentUserId = userId,
            Messages = thread,
            NewMessage = new SendMessageViewModel { BookingId = id }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(SendMessageViewModel model)
    {
        var userId = _userManager.GetUserId(User)!;

        var booking = await MyBookings(userId).FirstOrDefaultAsync(b => b.Id == model.BookingId);
        if (booking is null) return NotFound();

        if (booking.Status is BookingStatus.Rejected or BookingStatus.Cancelled)
        {
            TempData["Error"] = "This conversation is closed.";
            return RedirectToAction(nameof(Inbox));
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Message cannot be empty.";
            return RedirectToAction(nameof(Thread), new { id = model.BookingId });
        }

        _context.Messages.Add(new Message
        {
            BookingId = booking.Id,
            SenderId = userId,
            Content = model.Content.Trim()
        });
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Thread), new { id = model.BookingId });
    }
}
