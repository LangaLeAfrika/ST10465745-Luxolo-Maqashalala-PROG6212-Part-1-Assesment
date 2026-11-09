using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.ViewModels;

public class ProfileVm
{
    [Required]
    [StringLength(100)]
    [Display(Name = "First name")]
    public string FirstName { get; set; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "Last name")]
    public string LastName { get; set; } = "";

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = "";
}