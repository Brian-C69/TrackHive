using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;
using TrackHive.Services;

namespace TrackHive.Controllers;

[Authorize(Roles = "IT")]
public sealed class UsersController : Controller
{
    private readonly AppDbContext _db;
    private readonly SubscriptionUsageService _subscriptionUsage;

    public UsersController(AppDbContext db, SubscriptionUsageService subscriptionUsage)
    {
        _db = db;
        _subscriptionUsage = subscriptionUsage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q = null,
        int? orgId = null,
        RoleType? role = null,
        string? status = null,
        string? sortBy = "created",
        string? sortDir = "desc",
        int page = 1,
        int pageSize = 15)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 5 or > 100 ? 15 : pageSize;

        status = (status ?? "any").ToLowerInvariant();
        if (status is not ("any" or "active" or "inactive" or "locked" or "mustchangepassword"))
            status = "any";

        sortBy = (sortBy ?? "created").ToLowerInvariant();
        if (sortBy is not ("name" or "email" or "org" or "role" or "created" or "status"))
            sortBy = "created";

        sortDir = (sortDir ?? "desc").ToLowerInvariant();
        if (sortDir is not ("asc" or "desc"))
            sortDir = "desc";

        IQueryable<AppUser> query = _db.Users
            .Include(u => u.Organization)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(u => EF.Functions.Like(u.Name, pattern) || EF.Functions.Like(u.Email, pattern));
        }

        if (orgId.HasValue && orgId.Value > 0)
        {
            query = query.Where(u => u.OrganizationId == orgId.Value);
        }

        if (role.HasValue)
        {
            query = query.Where(u => u.Role == role.Value);
        }

        query = status switch
        {
            "active" => query.Where(u => u.IsActive),
            "inactive" => query.Where(u => !u.IsActive),
            "locked" => query.Where(u => u.IsLocked),
            "mustchangepassword" => query.Where(u => u.MustChangePassword),
            _ => query
        };

        query = (sortBy, sortDir) switch
        {
            ("name", "asc") => query.OrderBy(u => u.Name).ThenBy(u => u.Id),
            ("name", _) => query.OrderByDescending(u => u.Name).ThenByDescending(u => u.Id),
            ("email", "asc") => query.OrderBy(u => u.Email).ThenBy(u => u.Id),
            ("email", _) => query.OrderByDescending(u => u.Email).ThenByDescending(u => u.Id),
            ("org", "asc") => query.OrderBy(u => u.Organization!.Name).ThenBy(u => u.Id),
            ("org", _) => query.OrderByDescending(u => u.Organization!.Name).ThenByDescending(u => u.Id),
            ("role", "asc") => query.OrderBy(u => u.Role).ThenBy(u => u.Id),
            ("role", _) => query.OrderByDescending(u => u.Role).ThenByDescending(u => u.Id),
            ("status", "asc") => query.OrderBy(u => u.IsActive).ThenBy(u => u.Id),
            ("status", _) => query.OrderByDescending(u => u.IsActive).ThenByDescending(u => u.Id),
            ("created", "asc") => query.OrderBy(u => u.CreatedAt).ThenBy(u => u.Id),
            _ => query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id)
        };

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var rows = items.Select(u => new AdminUserRow
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Organization = u.Organization?.Name ?? "—",
            Role = u.Role,
            IsActive = u.IsActive,
            IsLocked = u.IsLocked,
            MustChangePassword = u.MustChangePassword,
            CreatedAt = u.CreatedAt
        }).ToList();

        var organizations = await _db.Organizations
            .OrderBy(o => o.Name)
            .Select(o => new OrganizationOption
            {
                Id = o.Id,
                Name = o.Name
            })
            .ToListAsync();

        var vm = new AdminUsersIndexViewModel
        {
            Users = rows,
            Organizations = organizations,
            Query = q,
            OrganizationId = orgId > 0 ? orgId : null,
            Role = role,
            Status = status,
            SortBy = sortBy,
            SortDir = sortDir,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new AdminUserFormViewModel
        {
            IsActive = true,
            MustChangePassword = true
        };
        await PopulateOrganizationsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminUserFormViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 8)
        {
            ModelState.AddModelError(nameof(AdminUserFormViewModel.Password), "Password must be at least 8 characters.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateOrganizationsAsync(model);
            return View(model);
        }

        var emailLower = model.Email.Trim().ToLowerInvariant();
        var exists = await _db.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
        if (exists)
        {
            ModelState.AddModelError(nameof(AdminUserFormViewModel.Email), "Email is already in use.");
            await PopulateOrganizationsAsync(model);
            return View(model);
        }

        var org = await _db.Organizations.FindAsync(model.OrganizationId);
        if (org is null)
        {
            ModelState.AddModelError(nameof(AdminUserFormViewModel.OrganizationId), "Organization not found.");
            await PopulateOrganizationsAsync(model);
            return View(model);
        }

        var limitCheck = await _subscriptionUsage.CheckCanAddUserAsync(org.Id, model.Role, HttpContext.RequestAborted);
        if (!limitCheck.CanAdd)
        {
            var message = limitCheck.BlockReason
                ?? "Invite blocked: your subscription plan has reached its seat limit. Visit Billing to upgrade.";
            ViewData["UpgradePrompt"] = message;
            ModelState.AddModelError(nameof(AdminUserFormViewModel.Role), message);
            await PopulateOrganizationsAsync(model);
            return View(model);
        }

        var user = new AppUser
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim(),
            Role = model.Role,
            OrganizationId = org.Id,
            IsActive = model.IsActive,
            MustChangePassword = model.MustChangePassword,
            PasswordHash = PasswordHasher.Hash(model.Password!.Trim())
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        TempData["Toast"] = $"Created user '{user.Name}'.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var model = new AdminUserFormViewModel
        {
            Id = user.Id,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role,
            OrganizationId = user.OrganizationId,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword
        };
        await PopulateOrganizationsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AdminUserFormViewModel model)
    {
        if (model.Id is null)
        {
            return NotFound();
        }

        if (!string.IsNullOrWhiteSpace(model.Password) && model.Password.Length < 8)
        {
            ModelState.AddModelError(nameof(AdminUserFormViewModel.Password), "Password must be at least 8 characters.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateOrganizationsAsync(model);
            return View(model);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == model.Id);
        if (user is null) return NotFound();

        var emailLower = model.Email.Trim().ToLowerInvariant();
        var emailInUse = await _db.Users
            .AnyAsync(u => u.Email.ToLower() == emailLower && u.Id != user.Id);
        if (emailInUse)
        {
            ModelState.AddModelError(nameof(AdminUserFormViewModel.Email), "Email is already in use.");
            await PopulateOrganizationsAsync(model);
            return View(model);
        }

        var org = await _db.Organizations.FindAsync(model.OrganizationId);
        if (org is null)
        {
            ModelState.AddModelError(nameof(AdminUserFormViewModel.OrganizationId), "Organization not found.");
            await PopulateOrganizationsAsync(model);
            return View(model);
        }

        user.Name = model.Name.Trim();
        user.Email = model.Email.Trim();
        user.Role = model.Role;
        user.OrganizationId = org.Id;
        user.IsActive = model.IsActive;
        user.MustChangePassword = model.MustChangePassword;
        if (!string.IsNullOrWhiteSpace(model.Password))
        {
            user.PasswordHash = PasswordHasher.Hash(model.Password.Trim());
            user.MustChangePassword = true;
        }

        await _db.SaveChangesAsync();

        TempData["Toast"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(
        int id,
        string? q,
        int? orgId,
        RoleType? role,
        string? status,
        string? sortBy,
        string? sortDir,
        int page = 1,
        int pageSize = 15)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
        }

        var meId = GetCurrentUserId();
        if (meId.HasValue && user.Id == meId.Value)
        {
            TempData["Error"] = "You cannot change your own activation status.";
            return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
        }

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();

        TempData["Toast"] = user.IsActive ? "Account activated." : "Account deactivated.";
        return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        int id,
        string? q,
        int? orgId,
        RoleType? role,
        string? status,
        string? sortBy,
        string? sortDir,
        int page = 1,
        int pageSize = 15)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
        }

        var meId = GetCurrentUserId();
        if (meId.HasValue && user.Id == meId.Value)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        TempData["Toast"] = "User deleted.";
        return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Bulk(
        string cmd,
        int[] ids,
        string? q,
        int? orgId,
        RoleType? role,
        string? status,
        string? sortBy,
        string? sortDir,
        int page = 1,
        int pageSize = 15)
    {
        if (ids is null || ids.Length == 0)
        {
            TempData["Error"] = "No users selected.";
            return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
        }

        cmd = (cmd ?? string.Empty).ToLowerInvariant();
        if (cmd is not ("activate" or "deactivate" or "delete"))
        {
            TempData["Error"] = "Unknown bulk action.";
            return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
        }

        var meId = GetCurrentUserId();
        var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();

        int skipped = 0;
        if (cmd == "delete")
        {
            var toRemove = new List<AppUser>();
            foreach (var u in users)
            {
                if (meId.HasValue && u.Id == meId.Value)
                {
                    skipped++;
                    continue;
                }
                toRemove.Add(u);
            }

            if (toRemove.Count > 0)
            {
                _db.Users.RemoveRange(toRemove);
                await _db.SaveChangesAsync();
                TempData["Toast"] = $"Deleted {toRemove.Count} user(s)." + (skipped > 0 ? $" Skipped {skipped}." : string.Empty);
            }
            else
            {
                TempData["Error"] = skipped > 0 ? "Cannot delete your own account." : "No users deleted.";
            }
        }
        else
        {
            bool newState = cmd == "activate";
            int changed = 0;
            foreach (var u in users)
            {
                if (meId.HasValue && u.Id == meId.Value)
                {
                    skipped++;
                    continue;
                }

                if (u.IsActive != newState)
                {
                    u.IsActive = newState;
                    changed++;
                }
            }

            if (changed > 0)
            {
                await _db.SaveChangesAsync();
                TempData["Toast"] = $"{(newState ? "Activated" : "Deactivated")} {changed} user(s)." +
                                    (skipped > 0 ? $" Skipped {skipped}." : string.Empty);
            }
            else
            {
                TempData["Error"] = skipped > 0 ? "Cannot change your own account state." : "No changes were required.";
            }
        }

        return RedirectToIndex(q, orgId, role, status, sortBy, sortDir, page, pageSize);
    }

    private async Task PopulateOrganizationsAsync(AdminUserFormViewModel model)
    {
        var items = await _db.Organizations
            .OrderBy(o => o.Name)
            .Select(o => new SelectListItem
            {
                Value = o.Id.ToString(),
                Text = o.Name
            })
            .ToListAsync();

        model.Organizations = items;
    }

    private RedirectToActionResult RedirectToIndex(
        string? q,
        int? orgId,
        RoleType? role,
        string? status,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        return RedirectToAction(nameof(Index), new
        {
            q,
            orgId = orgId.HasValue && orgId.Value > 0 ? orgId : null,
            role = role.HasValue ? (int?)role.Value : null,
            status,
            sortBy,
            sortDir,
            page,
            pageSize
        });
    }

    private int? GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idStr, out var id)) return id;
        return null;
    }
}
