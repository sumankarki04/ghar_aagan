using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

public class User
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string PasswordSalt { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    public UserRole Role { get; set; } = UserRole.Customer;

    // Providers start unverified; verification is now driven by admin KYC approval.
    public bool IsVerified { get; set; }

    // KYC (Know Your Customer) — providers submit documents, admin approves/rejects.
    public KycStatus KycStatus { get; set; } = KycStatus.NotSubmitted;
    public DateTime? KycSubmittedAt { get; set; }
    public DateTime? KycReviewedAt { get; set; }
    [MaxLength(500)]
    public string? KycRejectionReason { get; set; }

    public ICollection<KycDocument> KycDocuments { get; set; } = new List<KycDocument>();

    // Provider-only profile text. Null for customers/admins.
    [MaxLength(1000)]
    public string? Bio { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ServiceListing> Listings { get; set; } = new List<ServiceListing>();
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
