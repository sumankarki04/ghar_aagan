using System.ComponentModel.DataAnnotations;
using GharAagan.Services;

namespace GharAagan.Models.ViewModels;

public class ProviderProfileFormViewModel
{
    [Required]
    [StringLength(600)]
    [Display(Name = "Bio / Description")]
    public string Bio { get; set; } = string.Empty;

    [Range(0, 60)]
    [Display(Name = "Years of Experience")]
    public int YearsExperience { get; set; }

    [Range(50, 100000, ErrorMessage = "Hourly rate must be between NPR 50 and NPR 100,000.")]
    [Display(Name = "Hourly Rate (NPR)")]
    public decimal HourlyRate { get; set; }

    [Required]
    [Display(Name = "Service Category")]
    public int ServiceCategoryId { get; set; }

    [Required]
    [StringLength(120)]
    [Display(Name = "Location / Area (e.g. Kathmandu - Baneshwor)")]
    public string Location { get; set; } = string.Empty;
}

public class AvailabilitySlotInput
{
    public DayOfWeek Day { get; set; }
    public bool Enabled { get; set; }

    [DataType(DataType.Time)]
    public TimeOnly StartTime { get; set; } = new(9, 0);

    [DataType(DataType.Time)]
    public TimeOnly EndTime { get; set; } = new(18, 0);
}

public class AvailabilityFormViewModel
{
    public List<AvailabilitySlotInput> Slots { get; set; } = new();
}

public class ProviderDashboardViewModel
{
    public ProviderProfile? Profile { get; set; }
    public List<Booking> PendingBookings { get; set; } = new();
    public List<Booking> ActiveBookings { get; set; } = new();
    public List<Booking> History { get; set; } = new();
    public decimal TotalEarnings { get; set; }
    public int CompletedJobs { get; set; }
}

public class BrowseProvidersViewModel
{
    public ServiceCategory Category { get; set; } = null!;
    public string? LocationFilter { get; set; }
    public List<RankedProvider> Providers { get; set; } = new();
}

public class ProviderDetailsViewModel
{
    public ProviderProfile Profile { get; set; } = null!;
    public List<Review> Reviews { get; set; } = new();
    public List<ProviderAvailability> Availability { get; set; } = new();
}
