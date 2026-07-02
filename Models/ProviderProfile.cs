using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GharAagan.Models;

/// <summary>
/// Profile for a user in the Provider role. One-to-one with ApplicationUser.
/// IsVerified is toggled by an Admin after reviewing the provider.
/// AverageRating is recomputed whenever a new Review is saved.
/// </summary>
public class ProviderProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    [Required]
    [StringLength(600)]
    public string Bio { get; set; } = string.Empty;

    [Range(0, 60)]
    [Display(Name = "Years of Experience")]
    public int YearsExperience { get; set; }

    [Range(50, 100000)]
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Hourly Rate (NPR)")]
    public decimal HourlyRate { get; set; }

    [Required]
    [Display(Name = "Service Category")]
    public int ServiceCategoryId { get; set; }
    public ServiceCategory ServiceCategory { get; set; } = null!;

    /// <summary>City / area served, e.g. "Kathmandu - Baneshwor".</summary>
    [Required]
    [StringLength(120)]
    [Display(Name = "Location / Area")]
    public string Location { get; set; } = string.Empty;

    [Display(Name = "Verified")]
    public bool IsVerified { get; set; }

    /// <summary>Denormalized average of review ratings (0 when unreviewed).</summary>
    [Range(0, 5)]
    public double AverageRating { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<ProviderAvailability> Availabilities { get; set; } = new List<ProviderAvailability>();
}
