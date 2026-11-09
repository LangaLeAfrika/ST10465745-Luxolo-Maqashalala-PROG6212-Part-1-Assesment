using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

[MvcSessionAuthorize("Organiser", "Participant")]
public class ProfileController : Controller
{
    private readonly RaceDayApiClient _api;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        RaceDayApiClient api,
        ILogger<ProfileController> logger)
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
                "api/users/me");

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
                    "Loading profile returned {StatusCode}.",
                    response.StatusCode);

                return ApiUnavailable();
            }

            var profile = await _api.ReadAsync<ProfileDto>(response);

            if (profile is null)
            {
                return ApiUnavailable();
            }

            return View(new ProfileVm
            {
                FirstName = profile.FirstName,
                LastName = profile.LastName,
                Email = profile.Email
            });
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(ex, "Could not load the user's profile.");
            return ApiUnavailable();
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileVm model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new UpdateProfileRequest
        {
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Email = model.Email.Trim()
        };

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Put,
                "api/users/me",
                request);

            if (response.IsSuccessStatusCode)
            {
                HttpContext.Session.SetString(
                    "DisplayName",
                    request.FirstName);

                TempData["Success"] = "Your profile was updated successfully.";

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

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                ModelState.AddModelError(
                    nameof(model.Email),
                    "That email address is already used by another account.");
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                ModelState.AddModelError(
                    "",
                    "Check your name and email address, then try again.");
            }
            else
            {
                _logger.LogWarning(
                    "Updating profile returned {StatusCode}.",
                    response.StatusCode);

                ModelState.AddModelError(
                    "",
                    "We could not confirm the update. " +
                    "Reload your profile before trying again.");
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not confirm the profile update.");

            ModelState.AddModelError(
                "",
                "The connection was interrupted. The update may have been " +
                "saved. Reload your profile before trying again.");
        }

        return View(model);
    }

    private IActionResult ApiUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return View("~/Views/Shared/ServiceUnavailable.cshtml");
    }
}