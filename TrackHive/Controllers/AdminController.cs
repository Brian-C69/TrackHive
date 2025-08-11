using Microsoft.AspNetCore.Mvc;
using TrackHive.Models.Invites;
using TrackHive.Services.Email;
using TrackHive.Services.Passwords;

namespace TrackHive.Controllers
{
    // In real app: [Authorize(Roles="IT")]
    public class AdminController : Controller
    {
        private readonly IEmailSender _email;

        public AdminController(IEmailSender email) { _email = email; }

        [HttpGet]
        public IActionResult InviteHr() => View(new InviteHrViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteHr(InviteHrViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var tempPassword = TempPasswordGenerator.Generate();

            // TODO: persist HR user with tempPassword (hashed), role=HR, org=CurrentOrgId
            // _userService.CreateHr(vm.Email, vm.Name, tempPassword);

            // Send notification email (no verification, just info)
            var subject = "You’ve been added to TrackHive (HR Admin)";
            var body = $@"
                <p>Hi {vm.Name},</p>
                <p><strong>Your company has added you to TrackHive</strong> as an HR Admin.</p>
                <p><b>Login Email:</b> {vm.Email}<br/>
                   <b>Temporary Password:</b> {tempPassword}</p>
                <p>Please log in now and change your password immediately.</p>
                <p><a href=""{Url.Action("Login", "Account", null, Request.Scheme)}"">Log in to TrackHive</a></p>
                <p>— TrackHive</p>";
            await _email.SendAsync(vm.Email, subject, body);

            TempData["InviteSuccess"] = $"Invite sent to {vm.Email}.";
            return RedirectToAction(nameof(InviteHr));
        }
    }
}
