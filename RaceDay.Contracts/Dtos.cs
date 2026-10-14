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
    [Range(typeof(decimal), "0.01", "9999.99")]
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