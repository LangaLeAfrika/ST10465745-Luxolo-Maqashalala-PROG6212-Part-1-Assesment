using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

[MvcSessionAuthorize("Participant")]
public class ParticipantController : Controller
{
    private readonly RaceDayApiClient _api;
    private readonly ILogger<ParticipantController> _logger;

    public ParticipantController(
        RaceDayApiClient api,
        ILogger<ParticipantController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        string? eventType,
        string? location)
    {
        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                "api/events");

            if (!response.IsSuccessStatusCode)
            {
                return ApiUnavailable();
            }

            var events = await _api.ReadAsync<List<EventDto>>(response);

            if (events is null)
            {
                return ApiUnavailable();
            }

            var today = DateOnly.FromDateTime(
                DateTimeOffset.UtcNow
                    .ToOffset(TimeSpan.FromHours(2))
                    .DateTime);

            search = search?.Trim();
            eventType = eventType?.Trim();
            location = location?.Trim();

            var filtered = events.Where(e => e.EventDate >= today);

            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(e =>
                    e.EventName.Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(eventType))
            {
                filtered = filtered.Where(e =>
                    string.Equals(
                        e.EventType,
                        eventType,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                filtered = filtered.Where(e =>
                    e.Location.Contains(
                        location,
                        StringComparison.OrdinalIgnoreCase));
            }

            return View(new ParticipantEventsVm
            {
                Search = search,
                EventType = eventType,
                Location = location,
                Events = filtered
                    .OrderBy(e => e.EventDate)
                    .ThenBy(e => e.EventName)
                    .ToList()
            });
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(ex, "Could not load participant events.");
            return ApiUnavailable();
        }
    }

    // Display the event details and available categories.
    [HttpGet]
    public async Task<IActionResult> Enter(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            using var eventResponse = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{id}");

            if (eventResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (!eventResponse.IsSuccessStatusCode)
            {
                return ApiUnavailable();
            }

            var raceEvent =
                await _api.ReadAsync<EventDto>(eventResponse);

            if (raceEvent is null)
            {
                return ApiUnavailable();
            }

            using var categoriesResponse = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{id}/categories");

            if (categoriesResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (!categoriesResponse.IsSuccessStatusCode)
            {
                return ApiUnavailable();
            }

            var categories =
                await _api.ReadAsync<List<CategoryDto>>(categoriesResponse);

            if (categories is null)
            {
                return ApiUnavailable();
            }

            return View("Enter", new EnterEventVm
            {
                Event = raceEvent,
                Categories = categories
                    .OrderBy(c => c.CategoryName)
                    .ToList()
            });
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load event {EventId} for enrolment.",
                id);

            return ApiUnavailable();
        }
    }

    // Submit the participant's category choice.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enter(int id, EnterEventVm model)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return await Enter(id);
        }

        try
        {
            var request = new CreateEnrolmentRequest
            {
                CategoryId = model.CategoryId!.Value
            };

            using var response = await _api.SendAsync(
                HttpMethod.Post,
                $"api/events/{id}/enrolments",
                request);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                TempData["Success"] = "You have successfully entered the event.";
                return RedirectToAction(nameof(MyEvents));
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

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError(
                    "",
                    "You are already enrolled in this event.");
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                ModelState.AddModelError(
                    "",
                    "The event or selected category is no longer available.");
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError(
                    "",
                    "The entry could not be accepted. " +
                    "Check that your category belongs to this event.");
            }
            else
            {
                _logger.LogWarning(
                    "Enrolment for event {EventId} returned {StatusCode}.",
                    id,
                    response.StatusCode);

                ModelState.AddModelError(
                    "",
                    "We could not confirm your entry. Please check your " +
                    "enrolments before trying again.");
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not confirm enrolment for event {EventId}.",
                id);

            ModelState.AddModelError(
                "",
                "The connection was interrupted. Your entry may have been " +
                "saved. Please check your enrolments before trying again.");
        }

        // Reload display data while retaining submitted values and errors.
        return await Enter(id);
    }
    [HttpGet]
    public async Task<IActionResult> MyEvents()
    {
        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                "api/enrolments/me");

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
                    "Loading participant enrolments returned {StatusCode}.",
                    response.StatusCode);

                return ApiUnavailable();
            }

            var enrolments =
                await _api.ReadAsync<List<EnrolmentDto>>(response);

            if (enrolments is null)
            {
                return ApiUnavailable();
            }

            return View(enrolments
                .OrderByDescending(e => e.EnrolmentDate)
                .ToList());
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load participant enrolments.");

            return ApiUnavailable();
        }
    }
    [HttpGet]
    public async Task<IActionResult> MyResults()
    {
        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                "api/results/me");

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
                    "Loading participant results returned {StatusCode}.",
                    response.StatusCode);

                return ApiUnavailable();
            }

            var results =
                await _api.ReadAsync<List<ResultDto>>(response);

            if (results is null)
            {
                return ApiUnavailable();
            }

            return View(results
                     .OrderByDescending(r => r.EventDate)
                     .ThenBy(r => r.EventName)
                     .ThenBy(r => r.CategoryName)
                     .ToList());
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load participant results.");

            return ApiUnavailable();
        }
    }
    private IActionResult ApiUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("~/Views/Shared/ServiceUnavailable.cshtml");
    }
}