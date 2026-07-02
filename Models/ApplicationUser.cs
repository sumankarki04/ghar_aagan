using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models;

/// <summary>
/// Application user extending IdentityUser.
/// PhoneNumber is inherited from IdentityUser.
/// Roles: Customer, Provider, Admin (managed via ASP.NET Core Identity roles).
/// </summary>
public class ApplicationUser : IdentityUser
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Address { get; set; }

    public ProviderProfile? ProviderProfile { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
