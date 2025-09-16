using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using TrackHive.Models;

namespace TrackHive.Models;

/// <summary>
/// Enforces password change: any authenticated user with MustChangePassword=true
/// is redirected to Auth/ChangePassword (except that action and auth endpoints).
/// </summary>
public sealed class MustChangePasswordFilter : IAsyncAuthorizationFilter
{
    private readonly AppDbContext _db;
    public MustChangePasswordFilter(AppDbContext db) => _db = db;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        var user = http.User;

        // Skip if not authenticated
        if (user?.Identity?.IsAuthenticated != true) return;

        // Skip if [AllowAnonymous]
        var endpoint = http.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null)
            return;

        // Skip if current action is Auth/ChangePassword or Auth/Login/Logout to avoid loops
        if (context.ActionDescriptor is ControllerActionDescriptor cad)
        {
            var ctrl = cad.ControllerName;
            var action = cad.ActionName;
            if (ctrl == "Auth" && (action == "ChangePassword" || action == "Login" || action == "Logout"))
                return;
        }

        // Load current user
        var idStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id)) return;

        var dbUser = await _db.Users.FindAsync(id);
        if (dbUser is null) return;

        if (dbUser.MustChangePassword)
        {
            context.Result = new RedirectToActionResult("ChangePassword", "Auth", null);
        }
    }
}