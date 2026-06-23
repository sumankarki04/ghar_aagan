using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

public class ServiceCategory
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    public ICollection<ServiceListing> Listings { get; set; } = new List<ServiceListing>();
}
