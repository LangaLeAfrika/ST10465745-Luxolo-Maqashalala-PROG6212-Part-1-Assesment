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

    private IActionResult ApiUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return View("~/Views/Shared/ServiceUnavailable.cshtml");
    }
}