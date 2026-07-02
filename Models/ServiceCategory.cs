using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

/// <summary>
/// A category of home service (Plumbing, Electrical, Cleaning, ...).
/// Seeded on first run — see Data/SeedData.cs.
/// </summary>
public class ServiceCategory
{
    public int Id { get; set; }

    [Required]
    [StringLength(60)]
    public string Name { get; set; } = string.Empty;

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>Bootstrap Icons class used on category cards (e.g. "bi-droplet").</summary>
    [StringLength(50)]
    public string IconClass { get; set; } = "bi-tools";

    public ICollection<ProviderProfile> Providers { get; set; } = new List<ProviderProfile>();
}
