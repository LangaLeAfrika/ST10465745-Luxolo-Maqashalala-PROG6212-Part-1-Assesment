using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Contracts;
using RaceDay.Mvc.Services;

namespace RaceDay.Mvc.Filters;

public sealed class MvcSessionAuthorizationFilter
    : IAsyncAuthorizationFilter
{
    private readonly RaceDayApiClient _api;
    private readonly ILogger<MvcSessionAuthorizationFilter> _logger;
    private readonly string[] _roles;

    public MvcSessionAuthorizationFilter(
        RaceDayApiClient api,
        ILogger<MvcSessionAuthorizationFilter> logger,
        string[] roles)
    {
        _api = api;
        _logger = logger;
        _roles = roles;
    }

    public async Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;

        // Protected pages must not be stored in the browser cache.
        context.HttpContext.Response.Headers["Cache-Control"] = "no-store";

        await session.LoadAsync();

        if (string.IsNullOrWhiteSpace(
            session.GetString("ApiSessionCookie")))
        {
            RedirectToLogin(context);
            return;
        }

        try
        {
            using var response = await _api.SendAsync(
                HttpMethod.Get,
                "api/auth/session");

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                RedirectToLogin(context);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "API session check returned {StatusCode}.",
                    response.StatusCode);

                ShowUnavailable(context);
                return;
            }

            var user = await _api.ReadAsync<AuthResponse>(response);

            if (user is null ||
                user.UserId <= 0 ||
                (user.Role != "Organiser" &&
                 user.Role != "Participant"))
            {
                RedirectToLogin(context);
                return;
            }

            // Refresh MVC's display information from the API.
            session.SetInt32("UserId", user.UserId);
            session.SetString("Role", user.Role);
            session.SetString(
                "DisplayName",
                user.FirstName ?? user.Email ?? "RaceDay user");

            if (_roles.Length > 0 &&
                !_roles.Contains(user.Role, StringComparer.Ordinal))
            {
                context.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/AccessDenied.cshtml",
                    StatusCode = StatusCodes.Status403Forbidden
                };
            }
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            _logger.LogWarning(ex, "Could not verify the API session.");
            ShowUnavailable(context);
        }
    }

    private static void RedirectToLogin(
        AuthorizationFilterContext context)
    {
        context.HttpContext.Session.Clear();

        context.Result = new RedirectToActionResult(
            "Login", "Auth", routeValues: null);
    }

    private static void ShowUnavailable(
        AuthorizationFilterContext context)
    {
        context.Result = new ViewResult
        {
            ViewName = "~/Views/Shared/ServiceUnavailable.cshtml",
            StatusCode = StatusCodes.Status503ServiceUnavailable
        };
    }
}