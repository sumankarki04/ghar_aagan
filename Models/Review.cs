using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

public class Review
{
    public int Id { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    public int CustomerId { get; set; }
    public User? Customer { get; set; }

    public int ServiceListingId { get; set; }
    public ServiceListing? ServiceListing { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
