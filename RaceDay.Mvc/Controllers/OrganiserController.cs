using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;

namespace RaceDay.Mvc.Controllers;

[MvcSessionAuthorize("Organiser")]
public class OrganiserController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}