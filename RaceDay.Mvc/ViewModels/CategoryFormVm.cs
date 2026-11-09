using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RaceDay.Mvc.ViewModels;

public class CategoryFormVm : IValidatableObject
{
    public int CategoryId { get; set; }

    public int EventId { get; set; }

    [ValidateNever]
    public string EventName { get; set; } = "";

    [Required]
    [StringLength(100)]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = "";

    [Range(0, int.MaxValue)]
    [Display(Name = "Minimum age")]
    public int? MinimumAge { get; set; }

    [Range(0, int.MaxValue)]
    [Display(Name = "Maximum age")]
    public int? MaximumAge { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (MinimumAge.HasValue &&
            MaximumAge.HasValue &&
            MaximumAge.Value < MinimumAge.Value)
        {
            yield return new ValidationResult(
                "Maximum age must be greater than or equal to minimum age.",
                new[] { nameof(MaximumAge) });
        }
    }
}