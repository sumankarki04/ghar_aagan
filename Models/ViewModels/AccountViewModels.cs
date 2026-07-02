using System.ComponentModel.DataAnnotations;

namespace GharAagan.Models.ViewModels;

public class RegisterViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^9\d{9}$", ErrorMessage = "Enter a valid 10-digit Nepali mobile number starting with 9.")]
    [Display(Name = "Mobile Number")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>"Customer" or "Provider" — selected at registration.</summary>
    [Required]
    [Display(Name = "I want to")]
    public string Role { get; set; } = "Customer";
}

public class ManageProfileViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^9\d{9}$", ErrorMessage = "Enter a valid 10-digit Nepali mobile number starting with 9.")]
    [Display(Name = "Mobile Number")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    [Display(Name = "Default Address")]
    public string Address { get; set; } = string.Empty;

    // Display-only
    public string Email { get; set; } = string.Empty;
    public string Roles { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
