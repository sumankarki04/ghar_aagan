using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

public class Payment : IConcurrencyStamped
{
    public int Id { get; set; }

    public Guid RowVersion { get; set; }

    public int BookingId { get; set; }
    public Booking? Booking { get; set; }

    [Range(0, 1_000_000)]
    public decimal Amount { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    // Mock gateway: "Cash", "eSewa", "Khalti", etc.
    [MaxLength(40)]
    public string? Method { get; set; }

    [MaxLength(80)]
    public string? TransactionRef { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
