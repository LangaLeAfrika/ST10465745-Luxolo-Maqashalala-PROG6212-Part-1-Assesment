using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RaceDay.Contracts;
using RaceDay.Data;
using RaceDay.Models;

namespace RaceDay.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _context;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public AuthController(
        RaceDayDbContext context,
        IPasswordHasher<AppUser> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request)
    {
        // Store emails consistently.
        string email = request.Email.Trim().ToLowerInvariant();

        bool emailExists = await _context.Users
            .AnyAsync(u => u.Email == email);

        if (emailExists)
        {
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        // Verify the selected role exists and is allowed.
        var role = await _context.Roles
            .SingleOrDefaultAsync(r => r.RoleId == request.RoleId);

        if (role is null ||
            (role.RoleName != "Organiser" &&
             role.RoleName != "Participant"))
        {
            return BadRequest(new
            {
                message = "Select a valid Organiser or Participant role."
            });
        }

        var user = new AppUser
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            RoleId = role.RoleId
        };

        // Save the hash, never the original password.
        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password);

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sqlException &&
                  (sqlException.Number == 2601 ||
                   sqlException.Number == 2627))
        {
            // Handles simultaneous registrations using the same email.
            return Conflict(new
            {
                message = "An account with this email already exists."
            });
        }

        var response = new AuthResponse
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = role.RoleName
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }
}