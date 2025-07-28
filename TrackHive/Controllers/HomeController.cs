using Microsoft.AspNetCore.Mvc;

namespace TrackHive.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
