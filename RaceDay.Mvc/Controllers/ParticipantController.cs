using Microsoft.AspNetCore.Mvc;
using RaceDay.Mvc.Filters;

namespace RaceDay.Mvc.Controllers;

[MvcSessionAuthorize("Participant")]
public class ParticipantController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}