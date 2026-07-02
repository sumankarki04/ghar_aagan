using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

/// <summary>
/// A chat message inside a booking's conversation thread.
/// Only the booking's customer and the booking's provider may read/write.
/// (Pattern: per-job chat, as on Urban Company / TaskRabbit.)
/// </summary>
public class Message
{
    public int Id { get; set; }

    [Required]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required]
    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    [Required]
    [StringLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Set true when the other party opens the thread.</summary>
    public bool IsRead { get; set; }
}
