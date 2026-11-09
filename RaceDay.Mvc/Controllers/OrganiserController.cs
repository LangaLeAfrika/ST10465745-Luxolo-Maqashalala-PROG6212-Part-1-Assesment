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

    private IActionResult ApiUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return View("~/Views/Shared/ServiceUnavailable.cshtml");
    }
}