using Microsoft.AspNetCore.Mvc;
using TrackHive.Models.Auth;

namespace TrackHive.Controllers;

public class AccountController : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        // TODO: Replace with real auth (Identity/your service)
        var demoEmail = "admin@trackhive.local";
        var demoPass = "Pass@123";

        if (model.Email.Equals(demoEmail, System.StringComparison.OrdinalIgnoreCase)
            && model.Password == demoPass)
        {
            // In real app: sign-in user and set auth cookie/claims.
            return Redirect(returnUrl ?? Url.Action("Index", "Home")!);
        }

        // Wrong creds -> single message, no list
        ViewData["AuthError"] = "Incorrect email or password.";
        return View(model);
    }

    [HttpGet]
    public IActionResult Logout()
    {
        // TODO: sign-out real auth cookie
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new TrackHive.Models.Auth.RegisterOrganizationViewModel());
    }
}
