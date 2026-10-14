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