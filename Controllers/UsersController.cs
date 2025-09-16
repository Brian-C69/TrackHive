using Microsoft.AspNetCore.Mvc;

namespace TrackHive.Controllers
{
    public class UsersController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
