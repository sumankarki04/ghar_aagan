using System.ComponentModel.DataAnnotations;

namespace GharAagan.Dtos;

public class BookingRequest
{
    [Required]
    public int ServiceListingId { get; set; }

    [Required]
    public DateTime ScheduledAt { get; set; }

    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class BookingResponse
{
    public int Id { get; set; }
    public int ServiceListingId { get; set; }
    public string ServiceTitle { get; set; } = string.Empty;
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PayRequest
{
    [Required, MaxLength(40)]
    public string Method { get; set; } = "eSewa";
}
