using Azure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Data;
using RaceDay.Filters;
using RaceDay.Services;

namespace RaceDay.Controllers;

[ApiController]
public class ImagesController(RaceDayDbContext db, IImageStorage storage,
    ILogger<ImagesController> logger) : ControllerBase
{
    public const int MaximumImageBytes = 5 * 1024 * 1024;

    [HttpPost("api/events/{id:int}/banner")]
    [SessionAuthorize("Organiser")]
    [RequestSizeLimit(MaximumImageBytes + 65536)]
    public async Task<IActionResult> UploadBanner(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        var race = await db.Events.SingleOrDefaultAsync(e => e.EventId == id, cancellationToken);
        if (race is null) return NotFound();
        if (race.OrganiserId != HttpContext.Session.GetInt32("UserId")) return StatusCode(403);
        return await Save(file, $"events/{id}", race.BannerImagePath,
            path => race.BannerImagePath = path, cancellationToken);
    }

    [HttpPost("api/users/me/photo")]
    [SessionAuthorize]
    [RequestSizeLimit(MaximumImageBytes + 65536)]
    public async Task<IActionResult> UploadPhoto(IFormFile? file, CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        var user = await db.Users.SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user is null) return Unauthorized();
        return await Save(file, $"users/{user.UserId}", user.ProfileImagePath,
            path => user.ProfileImagePath = path, cancellationToken);
    }

    // Event banners are public, just like the event listings. Photos are self-only.
    [HttpGet("api/events/{id:int}/banner")]
    public async Task<IActionResult> Banner(int id, CancellationToken cancellationToken) =>
        await Read(await db.Events.Where(e => e.EventId == id)
            .Select(e => e.BannerImagePath).SingleOrDefaultAsync(cancellationToken), cancellationToken);

    [HttpGet("api/users/me/photo")]
    [SessionAuthorize]
    public async Task<IActionResult> Photo(CancellationToken cancellationToken) =>
        await Read(await db.Users.Where(u => u.UserId == HttpContext.Session.GetInt32("UserId"))
            .Select(u => u.ProfileImagePath).SingleOrDefaultAsync(cancellationToken), cancellationToken);

    private async Task<IActionResult> Save(IFormFile? file, string prefix, string? previous,
        Action<string> setPath, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0 || file.Length > MaximumImageBytes)
            return BadRequest(new { message = "Choose a PNG or JPEG image up to 5 MB." });
        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        // Do not trust browser MIME types or use client filenames as blob paths.
        var png = bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        var jpeg = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255
            && bytes[^2] == 255 && bytes[^1] == 217;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!(png && extension == ".png") && !(jpeg && extension is ".jpg" or ".jpeg"))
            return BadRequest(new { message = "Only PNG and JPEG files with matching image signatures are accepted." });
        var path = $"{prefix}/{Guid.NewGuid():N}{(png ? ".png" : ".jpg")}";
        try
        {
            await storage.UploadAsync(path, bytes, png ? "image/png" : "image/jpeg", cancellationToken);
            setPath(path);
            try { await db.SaveChangesAsync(cancellationToken); }
            catch
            {
                await Cleanup(path);
                throw;
            }
            if (previous is not null) await Cleanup(previous);
            return Ok(new { message = "Image saved successfully." });
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        {
            // Avoid logging credentials that may appear in SDK configuration exceptions.
            logger.LogWarning("Image upload unavailable ({FailureType}).", ex.GetType().Name);
            return Problem(statusCode: 503, title: "Image storage is unavailable. Please try again later.");
        }
    }

    private async Task Cleanup(string path)
    {
        try { await storage.DeleteAsync(path, CancellationToken.None); }
        catch (Exception ex) when (IsStorageFailure(ex))
        { logger.LogWarning("An unused image could not be removed ({FailureType}).", ex.GetType().Name); }
    }

    private async Task<IActionResult> Read(string? path, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        if (path is null) return NotFound();
        try
        {
            var image = await storage.ReadAsync(path, cancellationToken);
            if (image is null) return NotFound();
            return File(image.Bytes, image.ContentType);
        }
        catch (Exception ex) when (IsStorageFailure(ex))
        { return Problem(statusCode: 503, title: "Image storage is unavailable."); }
    }

    private static bool IsStorageFailure(Exception ex) =>
        ex is RequestFailedException or ImageStorageUnavailableException or HttpRequestException or TaskCanceledException;
}
