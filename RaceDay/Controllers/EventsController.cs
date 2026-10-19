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
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public EventsController(RaceDayDbContext context)
    {
        _context = context;
    }

    // Public: browse all events.
    [HttpGet]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EventDto>>> GetEvents()
    {
        var events = await _context.Events
            .AsNoTracking()
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.EventId)
            .Select(e => new EventDto
            {
                EventId = e.EventId,
                EventName = e.EventName,
                Description = e.Description,
                EventDate = e.EventDate,
                Location = e.Location,
                Distance = e.Distance,
                EventType = e.EventType,
                OrganiserId = e.OrganiserId
            })
            .ToListAsync();

        return Ok(events);
    }

    // Public: view one event.
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> GetEvent(int id)
    {
        var raceEvent = await _context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.EventId == id);

        if (raceEvent is null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        return Ok(ToDto(raceEvent));
    }

    // Organisers can view the events they own.
    [HttpGet("mine")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(List<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<EventDto>>> GetMyEvents()
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var events = await _context.Events
            .AsNoTracking()
            .Where(e => e.OrganiserId == organiserId)
            .OrderBy(e => e.EventDate)
            .ThenBy(e => e.EventId)
            .ToListAsync();

        return Ok(events.Select(ToDto).ToList());
    }

    [HttpPost]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> CreateEvent(
        [FromBody] CreateEventRequest request)
    {
        // Prevent SQL rounding values with more than two decimal places.
        if (decimal.Round(request.Distance, 2) != request.Distance)
        {
            return BadRequest(new
            {
                message = "Distance must have no more than two decimal places."
            });
        }

        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var raceEvent = new Event
        {
            EventName = request.EventName.Trim(),
            Description = request.Description?.Trim(),
            EventDate = request.EventDate!.Value,
            Location = request.Location.Trim(),
            Distance = request.Distance,
            EventType = request.EventType,
            OrganiserId = organiserId
        };

        _context.Events.Add(raceEvent);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetEvent),
            new { id = raceEvent.EventId },
            ToDto(raceEvent));
    }

    [HttpPut("{id:int}")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> UpdateEvent(
        int id,
        [FromBody] UpdateEventRequest request)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var raceEvent = await _context.Events
            .SingleOrDefaultAsync(e => e.EventId == id);

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
                message = "You can only update events you own."
            });
        }

        if (decimal.Round(request.Distance, 2) != request.Distance)
        {
            return BadRequest(new
            {
                message = "Distance must have no more than two decimal places."
            });
        }

        raceEvent.EventName = request.EventName.Trim();
        raceEvent.Description = request.Description?.Trim();
        raceEvent.EventDate = request.EventDate!.Value;
        raceEvent.Location = request.Location.Trim();
        raceEvent.Distance = request.Distance;
        raceEvent.EventType = request.EventType;

        await _context.SaveChangesAsync();

        return Ok(ToDto(raceEvent));
    }

    [HttpDelete("{id:int}")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var raceEvent = await _context.Events
            .SingleOrDefaultAsync(e => e.EventId == id);

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
                message = "You can only delete events you own."
            });
        }

        bool hasEnrolments = await _context.Enrolments
            .AnyAsync(e => e.EventId == id);

        if (hasEnrolments)
        {
            return Conflict(new
            {
                message = "An event with enrolments cannot be deleted."
            });
        }

        _context.Events.Remove(raceEvent);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sqlException &&
                  sqlException.Number == 547)
        {
            // A related record may have been added after the check.
            return Conflict(new
            {
                message = "The event cannot be deleted because related records exist."
            });
        }

        return NoContent();
    }

    // Converts a database model into the public response.
    private static EventDto ToDto(Event raceEvent)
    {
        return new EventDto
        {
            EventId = raceEvent.EventId,
            EventName = raceEvent.EventName,
            Description = raceEvent.Description,
            EventDate = raceEvent.EventDate,
            Location = raceEvent.Location,
            Distance = raceEvent.Distance,
            EventType = raceEvent.EventType,
            OrganiserId = raceEvent.OrganiserId
        };
    }
}