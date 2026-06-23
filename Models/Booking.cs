using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

public class Booking : IConcurrencyStamped
{
    public int Id { get; set; }

    public Guid RowVersion { get; set; }

    public int CustomerId { get; set; }
    public User? Customer { get; set; }

    public int ServiceListingId { get; set; }
    public ServiceListing? ServiceListing { get; set; }

    public DateTime ScheduledAt { get; set; }

    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Payment? Payment { get; set; }
    public Review? Review { get; set; }
}
