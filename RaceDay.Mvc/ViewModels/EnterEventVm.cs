using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using RaceDay.Contracts;

namespace RaceDay.Mvc.ViewModels;

public class EnterEventVm
{
    [ValidateNever]
    public EventDto Event { get; set; } = null!;

    [ValidateNever]
    public List<CategoryDto> Categories { get; set; } = new();

    [Required(ErrorMessage = "Select a category.")]
    [Range(1, int.MaxValue, ErrorMessage = "Select a valid category.")]
    [Display(Name = "Category")]
    public int? CategoryId { get; set; }
}