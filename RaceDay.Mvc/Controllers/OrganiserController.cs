using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;
using System.Globalization;

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
    // Display an event's categories and the add-category form.
    [HttpGet]
    public async Task<IActionResult> Categories(int id)
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

            var raceEvent = await _api.ReadAsync<EventDto>(eventResponse);

            if (raceEvent is null)
            {
                return ApiUnavailable();
            }

            if (raceEvent.OrganiserId !=
                HttpContext.Session.GetInt32("UserId"))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
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

            var model = new EventCategoriesVm
            {
                Event = raceEvent,
                Categories = categories
                    .OrderBy(c => c.CategoryName)
                    .ToList()
            };

            return View("Categories", model);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load categories for event {EventId}.",
                id);

            return ApiUnavailable();
        }
    }

    // Add a category through the API.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(
        int id,
        [Bind(Prefix = "NewCategory")] CreateCategoryRequest request)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        if (request.MinimumAge.HasValue &&
            request.MaximumAge.HasValue &&
            request.MaximumAge.Value < request.MinimumAge.Value)
        {
            ModelState.AddModelError(
                "NewCategory.MaximumAge",
                "Maximum age must be greater than or equal to minimum age.");
        }

        if (!ModelState.IsValid)
        {
            // Reload the event and category list.
            // ModelState preserves the submitted form values and errors.
            return await Categories(id);
        }

        request.CategoryName = request.CategoryName.Trim();

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Post,
                $"api/events/{id}/categories",
                request);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                TempData["Success"] = "Category added successfully.";

                return RedirectToAction(
                    nameof(Categories),
                    new { id });
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
                return NotFound();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError(
                    "NewCategory.CategoryName",
                    "This event already has a category with that name.");
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError(
                    "",
                    "Check the category name and age limits, then try again.");
            }
            else
            {
                _logger.LogWarning(
                    "Adding a category to event {EventId} returned {StatusCode}.",
                    id,
                    response.StatusCode);

                ModelState.AddModelError(
                    "",
                    "We could not confirm the save. " +
                    "Check the category list before trying again.");
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not confirm category creation for event {EventId}.",
                id);

            ModelState.AddModelError(
                "",
                "The connection was interrupted. " +
                "Check the category list before trying again.");
        }

        return await Categories(id);
    }
    [HttpGet]
    public async Task<IActionResult> Enrolments(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            // This API endpoint checks that the organiser owns the event.
            using var enrolmentsResponse = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{id}/enrolments");

            if (enrolmentsResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (enrolmentsResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (enrolmentsResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (!enrolmentsResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Loading enrolments for event {EventId} returned {StatusCode}.",
                    id,
                    enrolmentsResponse.StatusCode);

                return ApiUnavailable();
            }

            var enrolments =
                await _api.ReadAsync<List<EnrolmentDto>>(enrolmentsResponse);

            if (enrolments is null)
            {
                return ApiUnavailable();
            }

            // Load the event name and details for the page heading.
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

            var raceEvent = await _api.ReadAsync<EventDto>(eventResponse);

            if (raceEvent is null)
            {
                return ApiUnavailable();
            }

            if (raceEvent.OrganiserId !=
                HttpContext.Session.GetInt32("UserId"))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            var model = new EventEnrolmentsVm
            {
                Event = raceEvent,
                Enrolments = enrolments
                    .OrderBy(e => e.CategoryName)
                    .ThenBy(e => e.ParticipantName)
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
                "Could not load enrolments for event {EventId}.",
                id);

            return ApiUnavailable();
        }
    }
    // Display the result form for a participant in an owned event.
    [HttpGet]
    public async Task<IActionResult> CaptureResult(int eventId, int id)
    {
        if (eventId <= 0 || id <= 0)
        {
            return NotFound();
        }

        try
        {
            // The API checks ownership before returning this list.
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{eventId}/enrolments");

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
                return NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiUnavailable();
            }

            var enrolments =
                await _api.ReadAsync<List<EnrolmentDto>>(response);

            if (enrolments is null)
            {
                return ApiUnavailable();
            }

            var entry = enrolments.FirstOrDefault(
                e => e.EnrolmentId == id && e.EventId == eventId);

            if (entry is null)
            {
                return NotFound();
            }

            return View("CaptureResult", new CaptureResultVm
            {
                EventId = entry.EventId,
                EnrolmentId = entry.EnrolmentId,
                EventName = entry.EventName,
                ParticipantName = entry.ParticipantName,
                CategoryName = entry.CategoryName
            });
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load result form for enrolment {EnrolmentId}.",
                id);

            return ApiUnavailable();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CaptureResult(
        int eventId,
        int id,
        CaptureResultVm model)
    {
        if (eventId <= 0 ||
            id <= 0 ||
            eventId != model.EventId ||
            id != model.EnrolmentId)
        {
            return BadRequest();
        }

        // Reload trusted display data and verify the enrolment belongs
        // to this organiser's event before processing the form.
        var page = await CaptureResult(eventId, id);

        if (page is not ViewResult view ||
            view.Model is not CaptureResultVm trustedModel)
        {
            return page;
        }

        trustedModel.FinishTime = model.FinishTime;
        trustedModel.FinishingPosition = model.FinishingPosition;

        if (!ModelState.IsValid)
        {
            return View("CaptureResult", trustedModel);
        }

        var request = new CreateResultRequest
        {
            FinishTime = TimeSpan.ParseExact(
                model.FinishTime,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture),
            FinishingPosition = model.FinishingPosition!.Value
        };

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Post,
                $"api/enrolments/{id}/result",
                request);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                TempData["Success"] = "Participant result saved successfully.";

                return RedirectToAction(
                    nameof(Enrolments),
                    new { id = eventId });
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
                return NotFound();
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError(
                    "",
                    "A result has already been recorded for this entry.");
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError(
                    "",
                    "The API rejected this result. Results cannot be recorded " +
                    "for future events. Check the event date, time and position.");
            }
            else
            {
                _logger.LogWarning(
                    "Saving result for enrolment {EnrolmentId} returned {StatusCode}.",
                    id,
                    response.StatusCode);

                ModelState.AddModelError(
                    "",
                    "We could not confirm the save. Check the recorded results " +
                    "before trying again.");
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not confirm result creation for enrolment {EnrolmentId}.",
                id);

            ModelState.AddModelError(
                "",
                "The connection was interrupted. The result may have been saved. " +
                "Check the recorded results before trying again.");
        }

        return View("CaptureResult", trustedModel);
    }
    [HttpGet]
    public async Task<IActionResult> Results(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            // The API checks event ownership before returning results.
            using var resultsResponse = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{id}/results");

            if (resultsResponse.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }

            if (resultsResponse.StatusCode == HttpStatusCode.Forbidden)
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            if (resultsResponse.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound();
            }

            if (!resultsResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Loading results for event {EventId} returned {StatusCode}.",
                    id,
                    resultsResponse.StatusCode);

                return ApiUnavailable();
            }

            var results =
                await _api.ReadAsync<List<ResultDto>>(resultsResponse);

            if (results is null)
            {
                return ApiUnavailable();
            }

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

            var raceEvent = await _api.ReadAsync<EventDto>(eventResponse);

            if (raceEvent is null)
            {
                return ApiUnavailable();
            }

            if (raceEvent.OrganiserId !=
                HttpContext.Session.GetInt32("UserId"))
            {
                Response.StatusCode = StatusCodes.Status403Forbidden;
                return View("~/Views/Shared/AccessDenied.cshtml");
            }

            return View(new EventResultsVm
            {
                Event = raceEvent,
                Results = results
                    .OrderBy(r => r.FinishingPosition ?? int.MaxValue)
                    .ThenBy(r => r.ParticipantName)
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
                "Could not load results for event {EventId}.",
                id);

            return ApiUnavailable();
        }
    }
    [HttpGet]
    public async Task<IActionResult> EditResult(int eventId, int id)
    {
        if (eventId <= 0 || id <= 0)
        {
            return NotFound();
        }

        try
        {
            // This endpoint checks that the organiser owns the event.
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                $"api/events/{eventId}/results");

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
                return NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiUnavailable();
            }

            var results = await _api.ReadAsync<List<ResultDto>>(response);

            if (results is null)
            {
                return ApiUnavailable();
            }

            var result = results.FirstOrDefault(
                r => r.ResultId == id && r.EventId == eventId);

            if (result is null)
            {
                return NotFound();
            }

            return View("EditResult", new CaptureResultVm
            {
                ResultId = result.ResultId,
                EventId = result.EventId,
                EnrolmentId = result.EnrolmentId,
                EventName = result.EventName,
                ParticipantName = result.ParticipantName,
                CategoryName = result.CategoryName,
                FinishTime = result.FinishTime.HasValue
                    ? result.FinishTime.Value.ToString(@"hh\:mm\:ss")
                    : "",
                FinishingPosition = result.FinishingPosition
            });
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(
                ex,
                "Could not load result {ResultId} for editing.",
                id);

            return ApiUnavailable();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResult(
        int eventId,
        int id,
        CaptureResultVm model)
    {
        if (eventId <= 0 ||
            id <= 0 ||
            eventId != model.EventId ||
            id != model.ResultId)
        {
            return BadRequest();
        }

        // Reload the saved result and verify event ownership.
        var page = await EditResult(eventId, id);

        if (page is not ViewResult view ||
            view.Model is not CaptureResultVm trustedModel)
        {
            return page;
        }

        trustedModel.FinishTime = model.FinishTime;
        trustedModel.FinishingPosition = model.FinishingPosition;

        if (!ModelState.IsValid)
        {
            return View("EditResult", trustedModel);
        }

        var request = new UpdateResultRequest
        {
            FinishTime = TimeSpan.ParseExact(
                model.FinishTime,
                @"hh\:mm\:ss",
                CultureInfo.InvariantCulture),
            FinishingPosition = model.FinishingPosition!.Value
        };

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Put,
                $"api/results/{id}",
                request);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Result updated successfully.";

                return RedirectToAction(
                    nameof(Results),
                    new { id = eventId });
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
                return NotFound();
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError(
                    "",
                    "The API rejected the update. Check the finish time, " +
                    "position and event date. Results cannot be recorded " +
                    "for future events.");
            }
            else
            {
                _logger.LogWarning(
                    "Updating result {ResultId} returned {StatusCode}.",
                    id,
                    response.StatusCode);

                ModelState.AddModelError(
                    "",
                    "We could not confirm the update. " +
                    "Check the recorded results before trying again.");
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Could not confirm the update of result {ResultId}.",
                id);

            ModelState.AddModelError(
                "",
                "The connection was interrupted. The update may have been saved. " +
                "Check the recorded results before trying again.");
        }

        return View("EditResult", trustedModel);
    }
    private IActionResult ApiUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return View("~/Views/Shared/ServiceUnavailable.cshtml");
    }
}