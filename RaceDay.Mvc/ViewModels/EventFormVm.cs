using System.ComponentModel.DataAnnotations;

namespace RaceDay.Mvc.ViewModels;

public class EventFormVm : IValidatableObject
{
    public int EventId { get; set; }
    [Required]
    [StringLength(200)]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = "";

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Event date")]
    public DateOnly? EventDate { get; set; }

    [Required]
    [StringLength(255)]
    public string Location { get; set; } = "";

    [Required]
    [Range(typeof(decimal), "0.01", "9999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Distance (km)")]
    public decimal? Distance { get; set; }

    [Required]
    [RegularExpression(
        "^(Running|Walking|Cycling)$",
        ErrorMessage = "Select Running, Walking or Cycling.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "";

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (Distance.HasValue &&
            decimal.Round(Distance.Value, 2) != Distance.Value)
        {
            yield return new ValidationResult(
                "Distance must have no more than two decimal places.",
                new[] { nameof(Distance) });
        }
    }
}