using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

/// <summary>
/// Simple weekly availability: one row per day-of-week + time slot for a provider.
/// </summary>
public class ProviderAvailability
{
    public int Id { get; set; }

    [Required]
    public int ProviderProfileId { get; set; }
    public ProviderProfile ProviderProfile { get; set; } = null!;

    [Required]
    [Display(Name = "Day of Week")]
    public DayOfWeek Day { get; set; }

    [Required]
    [Display(Name = "Start Time")]
    public TimeOnly StartTime { get; set; }

    [Required]
    [Display(Name = "End Time")]
    public TimeOnly EndTime { get; set; }
}
