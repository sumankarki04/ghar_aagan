using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GharAagan.Models;

public enum BookingStatus
{
    Pending = 0,
    Confirmed = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Rejected = 5
}

/// <summary>
/// A service booking made by a Customer for a ProviderProfile.
/// Price is snapshotted from the provider's hourly rate at booking time.
/// </summary>
public class Booking
{
    public int Id { get; set; }

    [Required]
    public string CustomerId { get; set; } = string.Empty;
    public ApplicationUser Customer { get; set; } = null!;

    [Required]
    public int ProviderProfileId { get; set; }
    public ProviderProfile ProviderProfile { get; set; } = null!;

    [Required]
    [Display(Name = "Scheduled Date & Time")]
    public DateTime ScheduledDateTime { get; set; }

    [Required]
    [StringLength(250)]
    [Display(Name = "Service Address")]
    public string Address { get; set; } = string.Empty;

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    [StringLength(500)]
    public string? Notes { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Price (NPR)")]
    public decimal Price { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Review? Review { get; set; }
}
