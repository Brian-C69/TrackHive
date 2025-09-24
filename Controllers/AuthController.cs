// File: Controllers/AuthController.cs
using System;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Controllers;

public sealed class AuthController : Controller
{
    private const int MaxFailedAttempts = 3;

    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public AuthController(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    // -------- Login --------
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login() => View(new LoginViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var emailLower = model.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Organization)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower && u.IsActive);

        if (user is not null && user.IsLocked)
        {
            model.ErrorMessage = "Your account is locked due to multiple failed sign-ins. Click \"Forgot password\" to reset.";
            return View(model);
        }

        if (user is null || !PasswordHasher.Verify(model.Password, user.PasswordHash))
        {
            if (user is not null)
            {
                user.FailedLoginCount = Math.Min(user.FailedLoginCount + 1, MaxFailedAttempts);
                if (user.FailedLoginCount >= MaxFailedAttempts) user.IsLocked = true;
                await _db.SaveChangesAsync();
            }

            model.ErrorMessage = (user?.IsLocked ?? false)
                ? "Your account is locked due to multiple failed sign-ins. Click \"Forgot password\" to reset."
                : "Invalid email or password.";
            return View(model);
        }

        if (user.FailedLoginCount != 0 || user.IsLocked)
        {
            user.FailedLoginCount = 0;
            user.IsLocked = false;
            await _db.SaveChangesAsync();
        }

        await SignInAsync(user);

        if (user.MustChangePassword) return RedirectToAction(nameof(ChangePassword));
        return RedirectToRoleHome(user.Role);
    }

    // -------- Change Password --------
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View(model);

        if (!PasswordHasher.Verify(model.CurrentPassword, user.PasswordHash))
        {
            ModelState.AddModelError(nameof(ChangePasswordViewModel.CurrentPassword), "Current password is incorrect.");
            return View(model);
        }

        user.PasswordHash = PasswordHasher.Hash(model.NewPassword);
        user.MustChangePassword = false;
        user.FailedLoginCount = 0;
        user.IsLocked = false;

        await _db.SaveChangesAsync();

        TempData["Toast"] = "Password updated.";
        return RedirectToRoleHome(user.Role);
    }

    // -------- Logout --------
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // -------- Forgot / Reset Password (email link) --------
    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var emailLower = model.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower && u.IsActive);

        if (user != null)
        {
            var oldTokens = _db.PasswordResets.Where(p => p.UserId == user.Id && p.UsedAt == null && p.ExpiresAt > DateTime.UtcNow);
            _db.PasswordResets.RemoveRange(oldTokens);
            await _db.SaveChangesAsync();

            var token = GenerateToken();
            var reset = new PasswordReset { UserId = user.Id, Token = token, ExpiresAt = DateTime.UtcNow.AddHours(1) };
            _db.PasswordResets.Add(reset);
            await _db.SaveChangesAsync();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var link = $"{baseUrl}/Auth/ResetPassword?token={Uri.EscapeDataString(token)}";

            var subject = "TrackHive password reset request";
            var body = $@"<p>Hello,</p>
<p>We received a request to reset your TrackHive password.</p>
<p><a href=""{link}"">Reset your password</a> (link expires in 1 hour).</p>
<p>If you didn't request this, you can ignore this email.</p>
<p>— TrackHive</p>";

            await _email.SendAsync(user.Email, subject, body);
        }

        return View(new ForgotPasswordViewModel
        {
            InfoMessage = "If that email exists, a reset link has been sent."
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Missing token.");

        var rec = await _db.PasswordResets.Include(p => p.User).FirstOrDefaultAsync(p => p.Token == token);

        if (rec is null || rec.IsUsed)
        {
            return View(new ResetPasswordViewModel { Token = token, ErrorMessage = "This reset link is invalid or expired." });
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var rec = await _db.PasswordResets.Include(p => p.User).FirstOrDefaultAsync(p => p.Token == model.Token);

        if (rec is null || rec.IsUsed || rec.ExpiresAt < DateTime.UtcNow || rec.User is null || !rec.User.IsActive)
        {
            model.ErrorMessage = "This reset link is invalid or expired.";
            return View(model);
        }

        rec.User.PasswordHash = PasswordHasher.Hash(model.NewPassword);
        rec.User.MustChangePassword = false;
        rec.User.FailedLoginCount = 0;
        rec.User.IsLocked = false;
        rec.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        TempData["Toast"] = "Password has been reset. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    // -------- Access Denied --------
    [HttpGet]
    public IActionResult Denied() => Content("Access denied.");

    // -------- Register (IT creates Org; stays on page with SuccessMessage) --------
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var orgName = model.OrganizationName.Trim();
        var emailLower = model.Email.Trim().ToLowerInvariant();

        var orgExists = await _db.Organizations.AnyAsync(o => o.Name.ToLower() == orgName.ToLower());
        if (orgExists)
        {
            ModelState.AddModelError(nameof(RegisterViewModel.OrganizationName), "Organization name already exists.");
            return View(model);
        }

        var emailInUse = await _db.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
        if (emailInUse)
        {
            ModelState.AddModelError(nameof(RegisterViewModel.Email), "Email is already registered.");
            return View(model);
        }

        using var tx = await _db.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;
        var org = new Organization
        {
            Name = orgName,
            CreatedByEmail = model.Email.Trim(),
            CreatedAt = now,
            Plan = OrganizationPlan.Free,
            CurrentPlan = SubscriptionPlan.Free,
            SubscriptionPlan = SubscriptionPlan.Free,
            BillingPeriodStartUtc = now,
            CurrentPeriodEndsUtc = null,
            TrialEndsUtc = now.AddDays(14)
        };
        _db.Organizations.Add(org);
        await _db.SaveChangesAsync(); // org.Id available

        var itUser = new AppUser
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            PasswordHash = PasswordHasher.Hash(model.Password),
            Role = RoleType.IT,
            OrganizationId = org.Id,
            MustChangePassword = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _db.Users.Add(itUser);
        await _db.SaveChangesAsync();

        await tx.CommitAsync();

        // Stay on page and show success message
        ModelState.Clear();
        return View(new RegisterViewModel
        {
            SuccessMessage = $"Organization '{org.Name}' created. Please sign in with IT admin {itUser.Email}."
        });
    }

    // -------- Helpers --------
    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return null;
        return await _db.Users.FindAsync(id);
    }

    private async Task SignInAsync(AppUser user)
    {
        // fully-qualify to avoid any Claim/Identity type shadowing
        var theme = string.Equals(user.ThemePreference, "dark", StringComparison.OrdinalIgnoreCase)
            ? "dark"
            : "light";
        var language = LanguagePreferences.Normalize(user.LanguagePreference);
        var navOrder = user.NavigationOrder ?? string.Empty;

        var claims = new List<System.Security.Claims.Claim>
        {
            new(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(System.Security.Claims.ClaimTypes.Name, user.Name),
            new(System.Security.Claims.ClaimTypes.Email, user.Email),
            new(System.Security.Claims.ClaimTypes.Role, user.Role.ToString()),
            new(UserClaimTypes.OrganizationId, user.OrganizationId.ToString()),
            new(UserClaimTypes.ThemePreference, theme),
            new(UserClaimTypes.LanguagePreference, language)
        };

        if (!string.IsNullOrWhiteSpace(navOrder))
        {
            claims.Add(new(UserClaimTypes.NavigationOrder, navOrder));
        }

        var identity = new System.Security.Claims.ClaimsIdentity(
            claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new System.Security.Claims.ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private IActionResult RedirectToRoleHome(RoleType role) =>
        role switch
        {
            RoleType.IT => RedirectToAction("Index", "Dashboard"),
            RoleType.HR => RedirectToAction("Index", "HrDashboard"),
            RoleType.Employee => RedirectToAction("Index", "EmployeeDashboard"),
            _ => RedirectToAction("Index", "Home")
        };
}
