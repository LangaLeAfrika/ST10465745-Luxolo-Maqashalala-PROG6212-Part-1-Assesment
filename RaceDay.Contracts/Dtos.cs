using System;
using System.ComponentModel.DataAnnotations;

namespace RaceDay.Contracts;

// Information supplied when registering.
public class RegisterRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    // Matches the roles in your Part 1 SQL script:
    // 1 = Organiser, 2 = Participant.
    [Range(1, 2, ErrorMessage = "Select Organiser (1) or Participant (2).")]
    public int RoleId { get; set; }
}

// Information supplied when logging in.
public class LoginRequest
{
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

// Information returned after successful authentication.
public class AuthResponse
{
    public int UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}
// Profile information returned to the logged-in user.
public class ProfileDto
{
    public int UserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;
}

// Fields a user may change on their own profile.
public class UpdateProfileRequest
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
}

// Information supplied by an organiser when creating an event.
// Field lengths follow your Part 1 SQL script.
public class CreateEventRequest
{
    [Required]
    [StringLength(200)]
    public string EventName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateOnly? EventDate { get; set; }

    [Required]
    [StringLength(255)]
    public string Location { get; set; } = string.Empty;

    // Distance in kilometres; matches DECIMAL(6,2).
    [Range(
    typeof(decimal),
    "0.01",
    "9999.99",
    ParseLimitsInInvariantCulture = true,
    ConvertValueInInvariantCulture = true,
    ErrorMessage = "Distance must be between 0.01 and 9999.99 kilometres.")]
    public decimal Distance { get; set; }

    [Required]
    [StringLength(50)]
    [RegularExpression(
        "^(Running|Walking|Cycling)$",
        ErrorMessage = "Event type must be Running, Walking or Cycling.")]
    public string EventType { get; set; } = string.Empty;
}

// Updating an event uses the same fields and validation.
public class UpdateEventRequest : CreateEventRequest
{
}

// Event information returned by the API.
public class EventDto
{
    public int EventId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly EventDate { get; set; }

    public string Location { get; set; } = string.Empty;

    public decimal Distance { get; set; }

    public string EventType { get; set; } = string.Empty;

    public int OrganiserId { get; set; }
}
// Information supplied when creating an event category.
public class CreateCategoryRequest : IValidatableObject
{
    [Required]
    [StringLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int? MinimumAge { get; set; }

    [Range(0, int.MaxValue)]
    public int? MaximumAge { get; set; }

    // Compare the two ages when both are provided.
    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (MinimumAge.HasValue &&
            MaximumAge.HasValue &&
            MaximumAge.Value < MinimumAge.Value)
        {
            yield return new ValidationResult(
                "Maximum age must be greater than or equal to minimum age.",
                new[] { nameof(MaximumAge), nameof(MinimumAge) });
        }
    }
}

// Category updates use the same fields and validation.
public class UpdateCategoryRequest : CreateCategoryRequest
{
}

// Category information returned by the API.
public class CategoryDto
{
    public int CategoryId { get; set; }

    public int EventId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public int? MinimumAge { get; set; }

    public int? MaximumAge { get; set; }
}

// Your planned route supplies the event ID:
// POST /api/events/{eventId}/enrolments
// The participant ID will come from the session.
public class CreateEnrolmentRequest
{
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}

// Enrolment information returned by the API.
public class EnrolmentDto
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }

    public string ParticipantName { get; set; } = string.Empty;

    public int EventId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public DateTime EnrolmentDate { get; set; }
}

// Your planned route supplies the enrolment ID:
// POST /api/enrolments/{enrolmentId}/result
public class CreateResultRequest : IValidatableObject
{
    [Required]
    public TimeSpan? FinishTime { get; set; }

    [Required]
    [Range(1, int.MaxValue)]
    public int? FinishingPosition { get; set; }

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (FinishTime.HasValue)
        {
            TimeSpan time = FinishTime.Value;

            // Your SQL script uses TIME(0):
            // values below 24 hours, with whole-second precision.
            if (time <= TimeSpan.Zero ||
                time >= TimeSpan.FromDays(1))
            {
                yield return new ValidationResult(
                    "Finish time must be greater than zero and less than 24 hours.",
                    new[] { nameof(FinishTime) });
            }
            else if (time.Ticks % TimeSpan.TicksPerSecond != 0)
            {
                yield return new ValidationResult(
                    "Finish time must use whole seconds.",
                    new[] { nameof(FinishTime) });
            }
        }
    }
}

// Result updates use the same fields and validation.
public class UpdateResultRequest : CreateResultRequest
{
}

// Result information returned by the API.
public class ResultDto
{
    public int ResultId { get; set; }

    public int EnrolmentId { get; set; }

    public int EventId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string ParticipantName { get; set; } = string.Empty;

    public TimeSpan? FinishTime { get; set; }

    public int? FinishingPosition { get; set; }
}