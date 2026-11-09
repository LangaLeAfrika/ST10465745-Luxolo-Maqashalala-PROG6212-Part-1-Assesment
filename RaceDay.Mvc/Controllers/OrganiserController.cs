using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

[MvcSessionAuthorize("Organiser")]
public class OrganiserController : Controller
{
    private readonly RaceDayApiClient _api;
    private readonly ILogger<OrganiserController> _logger;

    public OrganiserController(
        RaceDayApiClient api,
        ILogger<OrganiserController> logger)
    {
        _api = api;
        _logger = logger;
    }

    // Display the organiser's events.
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                "api/events/mine");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Loading organiser events returned {StatusCode}.",
                    response.StatusCode);

                return ApiUnavailable();
            }

            var events =
                await _api.ReadAsync<List<EventDto>>(response);

            if (events is null)
            {
                _logger.LogWarning(
                    "The API returned an empty response body for organiser events.");

                return ApiUnavailable();
            }

            var today = DateOnly.FromDateTime(
                DateTimeOffset.UtcNow
                    .ToOffset(TimeSpan.FromHours(2))
                    .DateTime);

            var model = new OrganiserDashboardVm
            {
                Today = today,
                Events = events
                    .OrderBy(e => e.EventDate < today)
                    .ThenBy(e => e.EventDate)
                    .ThenBy(e => e.EventName)
                    .ToList()
            };

            return View(model);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load organiser events.");

            return ApiUnavailable();
        }
    }

    // Display the Create Event form.
    [HttpGet]
    public IActionResult Create()
    {
        var today = DateOnly.FromDateTime(
            DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(2))
                .DateTime);

        return View(new EventFormVm
        {
            EventDate = today.AddDays(1)
        });
    }

    // Submit the new event to the API.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormVm model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new CreateEventRequest
        {
            EventName = model.EventName.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            EventDate = model.EventDate!.Value,
            Location = model.Location.Trim(),
            Distance = model.Distance!.Value,
            EventType = model.EventType
        };

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Post,
                "api/events",
                request);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                TempData["Success"] = "Event created successfully.";
                return RedirectToAction(nameof(Index));
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError(
                    "",
                    "The API rejected the event details. Check the date, " +
                    "distance and required fields, then try again.");
            }
            else
            {
                _logger.LogWarning(
                    "Creating an event returned {StatusCode}.",
                    response.StatusCode);

                ModelState.AddModelError(
                    "",
                    "We could not confirm that the event was saved. " +
                    "Check your dashboard before trying again.");
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not confirm event creation.");

            ModelState.AddModelError(
                "",
                "The connection was interrupted. Check your dashboard " +
                "before trying again, as the event may have been saved.");
        }

        return View(model);
    }
    // Load an existing event into the edit form.
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{id}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiUnavailable();
            }

            var raceEvent = await _api.ReadAsync<EventDto>(response);

            if (raceEvent is null)
            {
                return ApiUnavailable();
            }

            // The event details endpoint is public, so check ownership
            // before displaying the organiser's edit form.
            var userId = HttpContext.Session.GetInt32("UserId");

            if (raceEvent.OrganiserId != userId)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var model = new EventFormVm
            {
                EventId = raceEvent.EventId,
                EventName = raceEvent.EventName,
                Description = raceEvent.Description,
                EventDate = raceEvent.EventDate,
                Location = raceEvent.Location,
                Distance = raceEvent.Distance,
                EventType = raceEvent.EventType
            };

            return View(model);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(ex, "Could not load event {EventId}.", id);

            return ApiUnavailable();
        }
    }

    // Send the updated event to the API.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EventFormVm model)
    {
        if (id <= 0 || id != model.EventId)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new UpdateEventRequest
        {
            EventName = model.EventName.Trim(),
            Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            EventDate = model.EventDate!.Value,
            Location = model.Location.Trim(),
            Distance = model.Distance!.Value,
            EventType = model.EventType
        };

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Put,
                $"api/events/{id}",
                request);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Event updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            // The API independently checks ownership when saving.
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError(
                    "",
                    "The API rejected the event details. Check the date, " +
                    "distance and required fields.");
            }
            else
            {
                _logger.LogWarning(
                    "Updating event {EventId} returned {StatusCode}.",
                    id,
                    response.StatusCode);

                ModelState.AddModelError(
                    "",
                    "We could not confirm the update. " +
                    "Check your dashboard before trying again.");
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not confirm update for event {EventId}.", id);

            ModelState.AddModelError(
                "",
                "The connection was interrupted. " +
                "Check your dashboard before trying again.");
        }

        return View(model);
    }
    // Show the confirmation page without deleting anything.
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{id}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiUnavailable();
            }

            var raceEvent = await _api.ReadAsync<EventDto>(response);

            if (raceEvent is null)
            {
                return ApiUnavailable();
            }

            var userId = HttpContext.Session.GetInt32("UserId");

            if (raceEvent.OrganiserId != userId)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            return View(raceEvent);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load event {EventId} for deletion.",
                id);

            return ApiUnavailable();
        }
    }

    // Delete only after the confirmation form is submitted.
    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Delete,
                $"api/events/{id}");

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                TempData["Success"] = "Event deleted successfully.";
                return RedirectToAction(nameof(Index));
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                TempData["Error"] =
                    "That event no longer exists. It may already have been deleted.";

                return RedirectToAction(nameof(Index));
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                TempData["Error"] =
                    "This event cannot be deleted because participants are enrolled.";

                return RedirectToAction(nameof(Index));
            }

            _logger.LogWarning(
                "Deleting event {EventId} returned {StatusCode}.",
                id,
                response.StatusCode);

            TempData["Error"] =
                "We could not confirm the deletion. " +
                "Check the event list before trying again.";
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not confirm deletion of event {EventId}.",
                id);

            TempData["Error"] =
                "The connection was interrupted. " +
                "Check the event list before trying again.";
        }

        return RedirectToAction(nameof(Index));
    }
    private IActionResult ApiUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return View("~/Views/Shared/ServiceUnavailable.cshtml");
    }
}