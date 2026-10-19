using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RaceDay.Contracts;
using RaceDay.Data;
using RaceDay.Filters;

namespace RaceDay.Controllers;

[ApiController]
[Route("api/users")]
[SessionAuthorize]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public UsersController(RaceDayDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDto>> GetMyProfile()
    {
        int? userId = HttpContext.Session.GetInt32("UserId");

        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Please log in."
            });
        }

        var profile = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId.Value)
            .Select(u => new ProfileDto
            {
                UserId = u.UserId,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Role = u.Role.RoleName
            })
            .SingleOrDefaultAsync();

        if (profile is null)
        {
            HttpContext.Session.Clear();

            return Unauthorized(new
            {
                message = "Your account is no longer available."
            });
        }

        return Ok(profile);
    }

    [HttpPut("me")]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileDto>> UpdateMyProfile(
        [FromBody] UpdateProfileRequest request)
    {
        int? userId = HttpContext.Session.GetInt32("UserId");

        if (!userId.HasValue)
        {
            return Unauthorized(new
            {
                message = "Please log in."
            });
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .SingleOrDefaultAsync(u => u.UserId == userId.Value);

        if (user is null)
        {
            HttpContext.Session.Clear();

            return Unauthorized(new
            {
                message = "Your account is no longer available."
            });
        }

        string email = request.Email.Trim().ToLowerInvariant();

        bool emailTaken = await _context.Users.AnyAsync(u =>
            u.Email == email && u.UserId != user.UserId);

        if (emailTaken)
        {
            return Conflict(new
            {
                message = "Another account already uses this email."
            });
        }

        // Only these three fields can be changed.
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Email = email;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sqlException &&
                  (sqlException.Number == 2601 ||
                   sqlException.Number == 2627))
        {
            return Conflict(new
            {
                message = "Another account already uses this email."
            });
        }

        return Ok(new ProfileDto
        {
            UserId = user.UserId,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Role = user.Role.RoleName
        });
    }
}