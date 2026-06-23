using System.ComponentModel.DataAnnotations;

namespace GharAagan.Dtos;

public class ReviewRequest
{
    [Required]
    public int BookingId { get; set; }

    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public class ReviewResponse
{
    public int Id { get; set; }
    public int ServiceListingId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
