using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

/// <summary>
/// A review left by the customer after a booking is Completed.
/// One review per booking (unique FK enforced in DbContext).
/// </summary>
public class Review
{
    public int Id { get; set; }

    [Required]
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; }

    [StringLength(500)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
