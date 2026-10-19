using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RaceDay.Contracts;
using RaceDay.Data;
using RaceDay.Filters;
using RaceDay.Models;

namespace RaceDay.Controllers;

[ApiController]
[Route("api")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _context;

    public CategoriesController(RaceDayDbContext context)
    {
        _context = context;
    }

    // Public: list categories belonging to an event.
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<CategoryDto>>> GetCategories(
        int eventId)
    {
        bool eventExists = await _context.Events
            .AnyAsync(e => e.EventId == eventId);

        if (!eventExists)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryName)
            .Select(c => new CategoryDto
            {
                CategoryId = c.CategoryId,
                EventId = c.EventId,
                CategoryName = c.CategoryName,
                MinimumAge = c.MinimumAge,
                MaximumAge = c.MaximumAge
            })
            .ToListAsync();

        return Ok(categories);
    }

    // Public: view a single category.
    [HttpGet("categories/{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.CategoryId == id);

        if (category is null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        return Ok(ToDto(category));
    }

    [HttpPost("events/{eventId:int}/categories")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> CreateCategory(
        int eventId,
        [FromBody] CreateCategoryRequest request)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var raceEvent = await _context.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.EventId == eventId);

        if (raceEvent is null)
        {
            return NotFound(new
            {
                message = "Event not found."
            });
        }

        if (raceEvent.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can only add categories to your own events."
            });
        }

        string categoryName = request.CategoryName.Trim();

        bool nameExists = await _context.Categories.AnyAsync(c =>
            c.EventId == eventId &&
            c.CategoryName == categoryName);

        if (nameExists)
        {
            return Conflict(new
            {
                message = "This event already has a category with that name."
            });
        }

        var category = new Category
        {
            EventId = eventId,
            CategoryName = categoryName,
            MinimumAge = request.MinimumAge,
            MaximumAge = request.MaximumAge
        };

        _context.Categories.Add(category);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627))
        {
            return Conflict(new
            {
                message = "This event already has a category with that name."
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number == 547)
        {
            // The event could have been deleted after our earlier check.
            return Conflict(new
            {
                message = "The related data changed. Refresh and try again."
            });
        }

        return CreatedAtAction(
            nameof(GetCategory),
            new { id = category.CategoryId },
            ToDto(category));
    }

    [HttpPut("categories/{id:int}")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> UpdateCategory(
        int id,
        [FromBody] UpdateCategoryRequest request)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var category = await _context.Categories
            .Include(c => c.Event)
            .SingleOrDefaultAsync(c => c.CategoryId == id);

        if (category is null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        if (category.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can only update categories for your own events."
            });
        }

        string categoryName = request.CategoryName.Trim();

        bool nameExists = await _context.Categories.AnyAsync(c =>
            c.EventId == category.EventId &&
            c.CategoryId != id &&
            c.CategoryName == categoryName);

        if (nameExists)
        {
            return Conflict(new
            {
                message = "This event already has a category with that name."
            });
        }

        category.CategoryName = categoryName;
        category.MinimumAge = request.MinimumAge;
        category.MaximumAge = request.MaximumAge;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "The category was removed while you were editing it."
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  (sql.Number == 2601 || sql.Number == 2627))
        {
            return Conflict(new
            {
                message = "This event already has a category with that name."
            });
        }

        return Ok(ToDto(category));
    }

    [HttpDelete("categories/{id:int}")]
    [SessionAuthorize("Organiser")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        int organiserId =
            HttpContext.Session.GetInt32("UserId")!.Value;

        var category = await _context.Categories
            .Include(c => c.Event)
            .SingleOrDefaultAsync(c => c.CategoryId == id);

        if (category is null)
        {
            return NotFound(new
            {
                message = "Category not found."
            });
        }

        if (category.Event.OrganiserId != organiserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "You can only delete categories for your own events."
            });
        }

        bool hasEnrolments = await _context.Enrolments
            .AnyAsync(e => e.CategoryId == id);

        if (hasEnrolments)
        {
            return Conflict(new
            {
                message = "A category with enrolments cannot be deleted."
            });
        }

        _context.Categories.Remove(category);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new
            {
                message = "The category has already been removed."
            });
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is SqlException sql &&
                  sql.Number == 547)
        {
            return Conflict(new
            {
                message = "The category cannot be deleted because related records exist."
            });
        }

        return NoContent();
    }

    private static CategoryDto ToDto(Category category)
    {
        return new CategoryDto
        {
            CategoryId = category.CategoryId,
            EventId = category.EventId,
            CategoryName = category.CategoryName,
            MinimumAge = category.MinimumAge,
            MaximumAge = category.MaximumAge
        };
    }
}