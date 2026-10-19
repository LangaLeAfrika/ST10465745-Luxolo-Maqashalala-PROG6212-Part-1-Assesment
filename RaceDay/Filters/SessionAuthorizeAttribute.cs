using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Data;

namespace RaceDay.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessionAuthorizeAttribute
    : Attribute, IAsyncAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public SessionAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles;
    }

    public async Task OnAuthorizationAsync(
        AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;

        await httpContext.Session.LoadAsync();

        int? userId = httpContext.Session.GetInt32("UserId");

        if (!userId.HasValue)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "Please log in to access this endpoint."
            });

            return;
        }

        var db = httpContext.RequestServices
            .GetRequiredService<RaceDayDbContext>();

        // Check the current database role instead of relying
        // only on a role saved during an earlier login.
        var account = await db.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId.Value)
            .Select(u => new
            {
                RoleName = u.Role.RoleName
            })
            .SingleOrDefaultAsync(httpContext.RequestAborted);

        if (account is null ||
            (account.RoleName != "Organiser" &&
             account.RoleName != "Participant"))
        {
            httpContext.Session.Clear();

            context.Result = new UnauthorizedObjectResult(new
            {
                message = "Your session is no longer valid. Please log in."
            });

            return;
        }

        httpContext.Session.SetString("Role", account.RoleName);

        if (_allowedRoles.Length > 0 &&
            !_allowedRoles.Contains(account.RoleName))
        {
            context.Result = new ObjectResult(new
            {
                message = "Your role cannot access this endpoint."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}