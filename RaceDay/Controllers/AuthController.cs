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
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
    [FromBody] LoginRequest request)
    {
        await HttpContext.Session.LoadAsync();

        // A new login attempt clears any previous signed-in account.
        HttpContext.Session.Clear();

        string email = request.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password."
            });
        }

        if (user.Role.RoleName != "Organiser" &&
            user.Role.RoleName != "Participant")
        {
            return Unauthorized(new
            {
                message = "This account does not have a supported role."
            });
        }

        // Refresh an older hash when the password hasher requests it.
        if (verificationResult ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(
                user,
                request.Password);

            await _context.SaveChangesAsync();
        }

        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("Role", user.Role.RoleName);

        await HttpContext.Session.CommitAsync();

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.RoleName
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.Session.LoadAsync();

        HttpContext.Session.Clear();

        await HttpContext.Session.CommitAsync();

        Response.Cookies.Delete(".RaceDay.Session", new CookieOptions
        {
            Path = "/",
            Secure = true,
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        });

        return Ok(new
        {
            message = "Logged out successfully."
        });
    }

    // Lets the client check whether its session is still signed in.
    [HttpGet("session")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> GetSession()
    {
        await HttpContext.Session.LoadAsync();

        int? userId = HttpContext.Session.GetInt32("UserId");

        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                message = "You are not logged in."
            });
        }

        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == userId.Value);

        if (user is null ||
            (user.Role.RoleName != "Organiser" &&
             user.Role.RoleName != "Participant"))
        {
            HttpContext.Session.Clear();

            return Unauthorized(new
            {
                message = "Your session is no longer valid. Please log in again."
            });
        }

        HttpContext.Session.SetString("Role", user.Role.RoleName);

        return Ok(new AuthResponse
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.RoleName
        });
    }
}