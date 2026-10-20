using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RaceDay.Contracts;
using RaceDay.Data;
using RaceDay.Filters;
using RaceDay.Models;

namespace RaceDay.Controllers;

[ApiController]
[Route("api")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public EnrolmentsController(RaceDayDbContext context)
    {
        _context = context;
    }

    [HttpPost("events/{eventId:int}/enrolments")]
    [SessionAuthorize("Participant")]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> CreateEnrolment(
        int eventId,
        [FromBody] CreateEnrolmentRequest request)
    {
        int participantId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var raceEvent = await _context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.EventId == eventId);

        if (raceEvent is null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        var category = await _context.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(c =>
                c.CategoryId == request.CategoryId);

        if (category is null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        // A valid category ID must also belong to this event.
        if (category.EventId != eventId)
        {
            return BadRequest(new
            {
                message = "The selected category does not belong to this event."
            });
        }

        bool alreadyEnrolled = await _context.Enrolments.AnyAsync(e =>
            e.ParticipantId == participantId &&
            e.EventId == eventId);

        if (alreadyEnrolled)
        {
            return Conflict(new
            {
                message = "You are already enrolled in this event."
            });
        }

        var participant = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == participantId);

        if (participant is null)
        {
            HttpContext.Session.Clear();

            return Unauthorized(new
            {
                message = "Your account is no longer available."
            });
        }

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            EventId = eventId,
            CategoryId = request.CategoryId

            // SQL Server supplies EnrolmentDate through its default.
        };

        _context.Enrolments.Add(enrolment);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627))
        {
            return Conflict(new
            {
                message = "You are already enrolled in this event."
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number == 547)
        {
            return Conflict(new
            {
                message = "The related data changed. Refresh and try again."
            });
        }

        return StatusCode(StatusCodes.Status201Created, new EnrolmentDto
        {
            EnrolmentId = enrolment.EnrolmentId,
            ParticipantId = participantId,
            ParticipantName =
                participant.FirstName + " " + participant.LastName,
            EventId = eventId,
            EventName = raceEvent.EventName,
            CategoryId = category.CategoryId,
            CategoryName = category.CategoryName,
            EnrolmentDate = enrolment.EnrolmentDate
        });
    }

    [HttpGet("enrolments/me")]
    [SessionAuthorize("Participant")]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EnrolmentDto>>> GetMyEnrolments()
    {
        int participantId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var enrolments = await ToDtos(
                _context.Enrolments
                    .AsNoTracking()
                    .Where(e => e.ParticipantId == participantId)
                    .OrderByDescending(e => e.EnrolmentDate)
                    .ThenByDescending(e => e.EnrolmentId))
            .ToListAsync();

        return Ok(enrolments);
    }

    [HttpGet("events/{eventId:int}/enrolments")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(List<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<EnrolmentDto>>> GetEventEnrolments(
        int eventId)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var raceEvent = await _context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.EventId == eventId);

        if (raceEvent is null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        if (raceEvent.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can only view enrolments for your own events."
            });
        }

        var enrolments = await ToDtos(
                _context.Enrolments
                    .AsNoTracking()
                    .Where(e => e.EventId == eventId)
                    .OrderBy(e => e.EnrolmentDate)
                    .ThenBy(e => e.EnrolmentId))
            .ToListAsync();

        return Ok(enrolments);
    }

    [HttpDelete("enrolments/{id:int}")]
    [SessionAuthorize("Participant")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEnrolment(int id)
    {
        int participantId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        // Search only this participant's entries.
        var enrolment = await _context.Enrolments
            .SingleOrDefaultAsync(e =>
                e.EnrolmentId == id &&
                e.ParticipantId == participantId);

        if (enrolment is null)
        {
            return NotFound(new
            {
                message = "Enrolment not found in your account."
            });
        }

        _context.Enrolments.Remove(enrolment);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "This enrolment has already been removed."
            });
        }

        return NoContent();
    }

    // Build a database query that returns only the DTO fields.
    private static IQueryable<EnrolmentDto> ToDtos(
        IQueryable<Enrolment> query)
    {
        return query.Select(e => new EnrolmentDto
        {
            EnrolmentId = e.EnrolmentId,
            ParticipantId = e.ParticipantId,
            ParticipantName =
                e.Participant.FirstName + " " + e.Participant.LastName,
            EventId = e.EventId,
            EventName = e.Event.EventName,
            CategoryId = e.CategoryId,
            CategoryName = e.Category.CategoryName,
            EnrolmentDate = e.EnrolmentDate
        });
    }
}