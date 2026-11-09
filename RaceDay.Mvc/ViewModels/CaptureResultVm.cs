using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace RaceDay.Mvc.ViewModels;

public class CaptureResultVm : IValidatableObject
{
    public int EventId { get; set; }

    public int EnrolmentId { get; set; }

    [ValidateNever]
    public string EventName { get; set; } = "";

    [ValidateNever]
    public string ParticipantName { get; set; } = "";

    [ValidateNever]
    public string CategoryName { get; set; } = "";

    [Required]
    [Display(Name = "Finish time")]
    public string FinishTime { get; set; } = "";

    [Required]
    [Range(1, int.MaxValue)]
    [Display(Name = "Finishing position")]
    public int? FinishingPosition { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(FinishTime) &&
            (!TimeSpan.TryParseExact(
                FinishTime,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture,
                out var time) ||
             time <= TimeSpan.Zero ||
             time >= TimeSpan.FromDays(1)))
        {
            yield return new ValidationResult(
                "Enter a time from 00:00:01 to 23:59:59 using HH:mm:ss.",
                new[] { nameof(FinishTime) });
        }
    }
}