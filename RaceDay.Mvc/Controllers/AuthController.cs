using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Contracts;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

public class AuthController : Controller
{
    private readonly RaceDayApiClient _api;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        RaceDayApiClient api,
        ILogger<AuthController> logger)
    {
        _api = api;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterVm());
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterVm model)
    {
        if (!ModelState.IsValid)
            return View(model);

        try
        {
            var request = new RegisterRequest
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email.Trim(),
                Password = model.Password,
                RoleId = model.RoleId
            };

            using var response = await _api.SendAsync(
                HttpMethod.Post,
                "api/auth/register",
                request);

            if (response.StatusCode == HttpStatusCode.Created)
            {
                TempData["Success"] =
                    "Registration successful. Please log in.";

                return RedirectToAction(nameof(Login));
            }

            var message = response.StatusCode switch
            {
                HttpStatusCode.Conflict =>
                    "An account with this email address already exists.",

                HttpStatusCode.BadRequest =>
                    "Please check your registration details.",

                _ => "Registration could not be completed. Please try again."
            };

            ModelState.AddModelError(string.Empty, message);
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Registration API request failed.");

            ModelState.AddModelError(
                string.Empty,
                "We could not reach the RaceDay service. Please try again.");
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetInt32("UserId").HasValue)
            return RedirectToAction("Index",
                HttpContext.Session.GetString("Role") == "Organiser"
                    ? "Organiser" : "Participant");

        return View(new LoginVm());
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginVm model)
    {
        if (HttpContext.Session.GetInt32("UserId").HasValue)
            return RedirectToAction("Index",
                HttpContext.Session.GetString("Role") == "Organiser"
                    ? "Organiser" : "Participant");

        if (!ModelState.IsValid)
            return View(model);

        // Remove any stale local authentication information.
        HttpContext.Session.Clear();

        try
        {
            var request = new LoginRequest
            {
                Email = model.Email.Trim(),
                Password = model.Password
            };

            using var response = await _api.SendAsync(
                HttpMethod.Post,
                "api/auth/login",
                request);

            if (!response.IsSuccessStatusCode)
            {
                HttpContext.Session.Clear();

                var message =
                    response.StatusCode == HttpStatusCode.Unauthorized
                        ? "The email address or password is incorrect."
                        : "Login could not be completed. Please try again.";

                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            var user = await _api.ReadAsync<AuthResponse>(response);

            if (user is null ||
                user.UserId <= 0 ||
                (user.Role != "Organiser" && user.Role != "Participant") ||
                string.IsNullOrWhiteSpace(
                    HttpContext.Session.GetString("ApiSessionCookie")))
            {
                HttpContext.Session.Clear();

                ModelState.AddModelError(
                    string.Empty,
                    "Login could not establish a valid session.");

                return View(model);
            }

            // Use identity and role returned by the API.
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("Role", user.Role);
            HttpContext.Session.SetString(
                "DisplayName",
                user.FirstName ?? user.Email ?? "RaceDay user");

            return RedirectToAction("Index",
                HttpContext.Session.GetString("Role") == "Organiser"
                    ? "Organiser" : "Participant");
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            HttpContext.Session.Clear();
            _logger.LogWarning(ex, "Login API request failed.");

            ModelState.AddModelError(
                string.Empty,
                "We could not reach the RaceDay service. Please try again.");

            return View(model);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Post,
                "api/auth/logout");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "API logout returned {StatusCode}.",
                    response.StatusCode);
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Logout API request failed.");
        }
        finally
        {
            // Clear MVC identity even if the API is unavailable.
            HttpContext.Session.Clear();
            Response.Cookies.Delete(".RaceDay.Mvc.Session");
        }

        return RedirectToAction(nameof(Login));
    }
}