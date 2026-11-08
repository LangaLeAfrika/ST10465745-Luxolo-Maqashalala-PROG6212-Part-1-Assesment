using Microsoft.AspNetCore.Mvc;

namespace RaceDay.Mvc.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class MvcSessionAuthorizeAttribute : TypeFilterAttribute
{
    public MvcSessionAuthorizeAttribute(params string[] roles)
        : base(typeof(MvcSessionAuthorizationFilter))
    {
        Arguments = new object[] { roles };
    }
}