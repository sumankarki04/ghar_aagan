using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models.ViewModels;

public class BookingCreateViewModel
{
    public int ProviderProfileId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }

    [Required]
    [Display(Name = "Preferred Date & Time")]
    public DateTime ScheduledDateTime { get; set; } = DateTime.Today.AddDays(1).AddHours(10);

    [Required]
    [StringLength(250)]
    [Display(Name = "Service Address")]
    public string Address { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Notes for the provider (optional)")]
    public string? Notes { get; set; }

    [Range(1, 24)]
    [Display(Name = "Estimated Hours")]
    public int EstimatedHours { get; set; } = 1;
}

public class ReviewCreateViewModel
{
    public int BookingId { get; set; }
    public string ProviderName { get; set; } = string.Empty;

    [Required]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
    public int Rating { get; set; } = 5;

    [StringLength(500)]
    public string? Comment { get; set; }
}

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalProviders { get; set; }
    public int TotalBookings { get; set; }
    public int PendingVerifications { get; set; }
    public int CompletedBookings { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalReviews { get; set; }
    public double PlatformAverageRating { get; set; }

    /// <summary>Booking count per status (for the status breakdown bar).</summary>
    public Dictionary<BookingStatus, int> StatusCounts { get; set; } = new();

    /// <summary>Provider + completed-booking count per category.</summary>
    public List<CategoryStat> CategoryStats { get; set; } = new();

    public List<Booking> RecentBookings { get; set; } = new();
}

public class CategoryStat
{
    public string Name { get; set; } = string.Empty;
    public string IconClass { get; set; } = "bi-tools";
    public int ProviderCount { get; set; }
    public int BookingCount { get; set; }
}

public class AdminUserRow
{
    public ApplicationUser User { get; set; } = null!;
    public string Roles { get; set; } = string.Empty;
    public bool IsSuspended { get; set; }
    public int BookingCount { get; set; }
}

public class CategoryFormViewModel
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.StringLength(250)]
    public string? Description { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50)]
    [System.ComponentModel.DataAnnotations.Display(Name = "Bootstrap Icon Class")]
    public string IconClass { get; set; } = "bi-tools";
}
