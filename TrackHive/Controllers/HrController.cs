using Microsoft.AspNetCore.Mvc;
using TrackHive.Models.Invites;
using TrackHive.Services.Email;
using TrackHive.Services.Passwords;

namespace TrackHive.Controllers
{
    // In real app: [Authorize(Roles="HR,IT")]
    public class HrController : Controller
    {
        private readonly IEmailSender _email;

        public HrController(IEmailSender email) { _email = email; }

        [HttpGet]
        public IActionResult InviteEmployee() => View(new InviteEmployeeViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InviteEmployee(InviteEmployeeViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var tempPassword = TempPasswordGenerator.Generate();

            // TODO: persist Employee user with tempPassword (hashed), role=Employee, org=CurrentOrgId, dept
            // _userService.CreateEmployee(vm.Email, vm.Name, tempPassword, vm.Department);

            var subject = "You’ve been added to TrackHive";
            var body = $@"
                <p>Hi {vm.Name},</p>
                <p><strong>{/*OrgName*/"Your company"}</strong> has added you to TrackHive.</p>
                <p><b>Login Email:</b> {vm.Email}<br/>
                   <b>Temporary Password:</b> {tempPassword}</p>
                <p>Please log in now and change your password immediately.</p>
                <p><a href=""{Url.Action("Login", "Account", null, Request.Scheme)}"">Log in to TrackHive</a></p>
                <p>— TrackHive</p>";
            await _email.SendAsync(vm.Email, subject, body);

            TempData["InviteSuccess"] = $"Invite sent to {vm.Email}.";
            return RedirectToAction(nameof(InviteEmployee));
        }
    }
}
