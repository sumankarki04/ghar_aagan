using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

public class ServiceListing
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Range(0, 1_000_000)]
    public decimal Price { get; set; }

    [MaxLength(80)]
    public string City { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Provider who owns this listing
    public int ProviderId { get; set; }
    public User? Provider { get; set; }

    public int CategoryId { get; set; }
    public ServiceCategory? Category { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}
