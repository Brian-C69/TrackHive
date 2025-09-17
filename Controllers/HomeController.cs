using Microsoft.AspNetCore.Mvc;

namespace TrackHive.Controllers;

public sealed class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public IActionResult Onboarding() => View();
}
