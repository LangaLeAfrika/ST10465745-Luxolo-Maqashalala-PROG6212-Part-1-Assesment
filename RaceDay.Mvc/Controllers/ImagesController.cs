using System.Net;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;
using RaceDay.Mvc.Services;
using RaceDay.Mvc.ViewModels;

namespace RaceDay.Mvc.Controllers;

public class ImagesController(RaceDayApiClient api) : Controller
{
    [HttpGet]
    [MvcSessionAuthorize("Organiser")]
    public IActionResult BannerUpload(int id) => View("Upload", new ImageUploadVm { EventId = id });

    [HttpGet]
    [MvcSessionAuthorize("Organiser", "Participant")]
    public IActionResult PhotoUpload() => View("Upload", new ImageUploadVm());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MvcSessionAuthorize("Organiser")]
    [RequestSizeLimit(5 * 1024 * 1024 + 65536)]
    public Task<IActionResult> BannerUpload(int id, ImageUploadVm model)
    {
        model.EventId = id;
        return Upload(model, $"api/events/{id}/banner");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MvcSessionAuthorize("Organiser", "Participant")]
    [RequestSizeLimit(5 * 1024 * 1024 + 65536)]
    public Task<IActionResult> PhotoUpload(ImageUploadVm model)
    {
        model.EventId = null;
        return Upload(model, "api/users/me/photo");
    }

    private async Task<IActionResult> Upload(ImageUploadVm model, string route)
    {
        if (model.File is null || model.File.Length == 0 || model.File.Length > 5 * 1024 * 1024)
        {
            ModelState.AddModelError("File", "Choose a PNG or JPEG image up to 5 MB.");
            return View("Upload", model);
        }
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StreamContent(model.File.OpenReadStream()), "file", Path.GetFileName(model.File.FileName));
            using var response = await api.SendAsync(HttpMethod.Post, route, content, HttpContext.RequestAborted);
            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Image saved successfully.";
                return model.EventId.HasValue
                    ? RedirectToAction(nameof(BannerUpload), new { id = model.EventId })
                    : RedirectToAction("Index", "Profile");
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Auth");
            }
            ModelState.AddModelError("", response.StatusCode switch
            {
                HttpStatusCode.Forbidden => "You can only change banners for events you organise.",
                HttpStatusCode.NotFound => "That event no longer exists.",
                HttpStatusCode.BadRequest => "Choose a valid PNG or JPEG file with a matching file extension.",
                HttpStatusCode.RequestEntityTooLarge => "The image must be no larger than 5 MB.",
                _ => "Image storage is unavailable. Please try again later."
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { ModelState.AddModelError("", "The connection was interrupted. Check your image before trying again."); }
        return View("Upload", model);
    }

    [HttpGet]
    public Task<IActionResult> Banner(int id) => Read($"api/events/{id}/banner", "event");

    [HttpGet]
    [MvcSessionAuthorize("Organiser", "Participant")]
    public Task<IActionResult> Photo() => Read("api/users/me/photo", "profile");

    private async Task<IActionResult> Read(string route, string kind)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        try
        {
            using var response = await api.SendAsync(HttpMethod.Get, route, cancellationToken: HttpContext.RequestAborted);
            if (response.IsSuccessStatusCode)
                return File(await response.Content.ReadAsByteArrayAsync(),
                    response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Redirect($"~/images/{kind}-placeholder.svg");
            return StatusCode((int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { return StatusCode(503); }
    }
}
