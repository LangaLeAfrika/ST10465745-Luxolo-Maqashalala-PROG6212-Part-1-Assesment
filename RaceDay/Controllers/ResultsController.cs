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
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public ResultsController(RaceDayDbContext context)
    {
        _context = context;
    }

    [HttpPost("enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> CreateResult(
        int enrolmentId,
        [FromBody] CreateResultRequest request)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var enrolment = await _context.Enrolments
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Include(e => e.Participant)
            .SingleOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment is null)
        {
            return NotFound(new
            {
                message = "Enrolment not found."
            });
        }

        if (enrolment.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can only capture results for your own events."
            });
        }

        // EventDate has no finish time, so this rule allows
        // capturing on the event date or later.
        if (enrolment.Event.EventDate > TodayInSouthAfrica())
        {
            return BadRequest(new
            {
                message = "Results cannot be captured before the event date."
            });
        }

        bool resultExists = await _context.Results
            .AnyAsync(r => r.EnrolmentId == enrolmentId);

        if (resultExists)
        {
            return Conflict(new
            {
                message = "This enrolment already has a result. Use the update endpoint."
            });
        }

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            Enrolment = enrolment,
            FinishTime = request.FinishTime,
            FinishingPosition = request.FinishingPosition
        };

        _context.Results.Add(result);

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
                message = "This enrolment already has a result."
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

        return StatusCode(
            StatusCodes.Status201Created,
            ToDto(result));
    }

    [HttpPut("results/{id:int}")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> UpdateResult(
        int id,
        [FromBody] UpdateResultRequest request)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var result = await _context.Results
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Event)
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Category)
            .Include(r => r.Enrolment)
                .ThenInclude(e => e.Participant)
            .SingleOrDefaultAsync(r => r.ResultId == id);

        if (result is null)
        {
            return NotFound(new
            {
                message = "Result not found."
            });
        }

        if (result.Enrolment.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can only update results for your own events."
            });
        }

        if (result.Enrolment.Event.EventDate > TodayInSouthAfrica())
        {
            return BadRequest(new
            {
                message = "Results cannot be updated before the event date."
            });
        }

        result.FinishTime = request.FinishTime;
        result.FinishingPosition = request.FinishingPosition;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "This result was removed while you were editing it."
            });
        }

        return Ok(ToDto(result));
    }

    [HttpGet("results/me")]
    [SessionAuthorize("Participant")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<ResultDto>>> GetMyResults()
    {
        int participantId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var results = await ProjectResults(
                _context.Results
                    .AsNoTracking()
                    .Where(r =>
                        r.Enrolment.ParticipantId == participantId)
                    .OrderByDescending(r =>
                        r.Enrolment.Event.EventDate)
                    .ThenByDescending(r => r.ResultId))
            .ToListAsync();

        return Ok(results);
    }

    [HttpGet("events/{eventId:int}/results")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(List<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ResultDto>>> GetEventResults(
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
                message = "You can only view results for your own events."
            });
        }

        var results = await ProjectResults(
                _context.Results
                    .AsNoTracking()
                    .Where(r => r.Enrolment.EventId == eventId)
                    .OrderBy(r => r.FinishingPosition)
                    .ThenBy(r => r.ResultId))
            .ToListAsync();

        return Ok(results);
    }

    private static ResultDto ToDto(Result result)
    {
        return new ResultDto
        {
            ResultId = result.ResultId,
            EnrolmentId = result.EnrolmentId,
            EventId = result.Enrolment.EventId,
            EventName = result.Enrolment.Event.EventName,
            CategoryName = result.Enrolment.Category.CategoryName,
            ParticipantName =
                result.Enrolment.Participant.FirstName + " " +
                result.Enrolment.Participant.LastName,
            FinishTime = result.FinishTime,
            FinishingPosition = result.FinishingPosition
        };
    }

    private static IQueryable<ResultDto> ProjectResults(
        IQueryable<Result> query)
    {
        return query.Select(r => new ResultDto
        {
            ResultId = r.ResultId,
            EnrolmentId = r.EnrolmentId,
            EventId = r.Enrolment.EventId,
            EventName = r.Enrolment.Event.EventName,
            CategoryName = r.Enrolment.Category.CategoryName,
            ParticipantName =
                r.Enrolment.Participant.FirstName + " " +
                r.Enrolment.Participant.LastName,
            FinishTime = r.FinishTime,
            FinishingPosition = r.FinishingPosition
        });
    }

    private static DateOnly TodayInSouthAfrica()
    {
        // South African Standard Time is UTC+02:00.
        return DateOnly.FromDateTime(
            DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(2))
                .DateTime);
    }
}