using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Controllers;

[Authorize]
public sealed class LeaveDocumentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public LeaveDocumentsController(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser is null) return RedirectToAction("Login", "Auth");

        var document = await _db.LeaveDocuments
            .Include(d => d.LeaveRequest)
            .ThenInclude(r => r.User)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document is null || document.LeaveRequest is null)
        {
            return NotFound();
        }

        var requestOwner = document.LeaveRequest.User ?? await _db.Users.FindAsync(document.LeaveRequest.UserId);
        if (requestOwner is null)
        {
            return NotFound();
        }

        var sameOrganization = requestOwner.OrganizationId == currentUser.OrganizationId;
        var isOwner = requestOwner.Id == currentUser.Id;
        var canHrAccess = currentUser.Role == RoleType.HR && sameOrganization;
        var canItAccess = currentUser.Role == RoleType.IT;

        if (!isOwner && !canHrAccess && !canItAccess)
        {
            return Forbid();
        }

        var physicalPath = Path.Combine(GetWebRootPath(), document.StoredFilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(physicalPath))
        {
            return NotFound();
        }

        var downloadName = string.IsNullOrWhiteSpace(document.OriginalFileName)
            ? "document"
            : document.OriginalFileName;
        var contentType = string.IsNullOrWhiteSpace(document.ContentType)
            ? "application/octet-stream"
            : document.ContentType;

        return PhysicalFile(physicalPath, contentType, downloadName);
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idStr, out var id))
        {
            return null;
        }

        return await _db.Users.FindAsync(id);
    }

    private string GetWebRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            return _environment.WebRootPath!;
        }

        var path = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Directory.CreateDirectory(path);
        return path;
    }
}
