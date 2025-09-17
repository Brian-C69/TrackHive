using System.IO;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using TrackHive.Models;

namespace TrackHive.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private const long MaxUploadBytes = 5 * 1024 * 1024;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public ProfileController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction("Login", "Auth");

        var model = new ProfileViewModel
        {
            Name = user.Name,
            Email = user.Email,
            BirthDate = user.BirthDate,
            PhoneNumber = user.PhoneNumber,
            JobTitle = user.JobTitle,
            Address = user.Address,
            About = user.About,
            ExistingImagePath = user.ProfileImagePath,
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ProfileViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null) return RedirectToAction("Login", "Auth");

        model.Email = user.Email;
        model.ExistingImagePath = user.ProfileImagePath;

        if (model.ProfileImage is { Length: > MaxUploadBytes })
        {
            ModelState.AddModelError(nameof(ProfileViewModel.ProfileImage), "Profile image must be 5 MB or smaller.");
        }

        if (model.ProfileImage is { } file && file.Length > 0 && !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(ProfileViewModel.ProfileImage), "Please upload a valid image file.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        user.Name = model.Name.Trim();
        user.BirthDate = model.BirthDate;
        user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        user.JobTitle = string.IsNullOrWhiteSpace(model.JobTitle) ? null : model.JobTitle.Trim();
        user.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
        user.About = string.IsNullOrWhiteSpace(model.About) ? null : model.About.Trim();

        var hasNewUpload = model.ProfileImage is { Length: > 0 };
        if (hasNewUpload)
        {
            model.RemoveImage = false;
        }

        if (model.RemoveImage)
        {
            DeleteProfileImage(user.ProfileImagePath);
            user.ProfileImagePath = null;
        }
        else if (hasNewUpload && model.ProfileImage is not null)
        {
            var oldPath = user.ProfileImagePath;
            var savedPath = await SaveProfileImageAsync(user, model);
            if (!string.IsNullOrEmpty(savedPath))
            {
                user.ProfileImagePath = savedPath;
                DeleteProfileImage(oldPath);
            }
        }

        await _db.SaveChangesAsync();
        await RefreshSignInAsync(user);

        TempData["Toast"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var id)) return null;
        return await _db.Users.FindAsync(id);
    }

    private async Task RefreshSignInAsync(AppUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("OrgId", user.OrganizationId.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = true });
    }

    private async Task<string?> SaveProfileImageAsync(AppUser user, ProfileViewModel model)
    {
        if (model.ProfileImage is null || model.ProfileImage.Length == 0)
        {
            return null;
        }

        await using var stream = model.ProfileImage.OpenReadStream();
        using var image = await Image.LoadAsync<Rgba32>(stream);

        var cropRect = CalculateCropRectangle(image.Width, image.Height, model);

        image.Mutate(ctx =>
        {
            if (cropRect.Width > 0 && cropRect.Height > 0)
            {
                ctx.Crop(cropRect);
            }

            ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(512, 512),
                Sampler = KnownResamplers.Lanczos3
            });
        });

        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"user-{user.Id}-{Guid.NewGuid():N}.jpg";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await image.SaveAsJpegAsync(filePath, new JpegEncoder { Quality = 85 });

        var relativePath = $"/uploads/profiles/{fileName}";
        model.ExistingImagePath = relativePath;
        return relativePath;
    }

    private static Rectangle CalculateCropRectangle(int imageWidth, int imageHeight, ProfileViewModel model)
    {
        if (model.CropWidth is null || model.CropHeight is null || model.CropWidth <= 0 || model.CropHeight <= 0)
        {
            return new Rectangle(0, 0, imageWidth, imageHeight);
        }

        var x = (int)Math.Round(model.CropX ?? 0);
        var y = (int)Math.Round(model.CropY ?? 0);
        var width = (int)Math.Round(model.CropWidth.Value);
        var height = (int)Math.Round(model.CropHeight.Value);

        var imageRect = new Rectangle(0, 0, imageWidth, imageHeight);
        var requested = new Rectangle(x, y, width, height);
        var crop = Rectangle.Intersect(imageRect, requested);

        return crop.Width > 0 && crop.Height > 0 ? crop : imageRect;
    }

    private void DeleteProfileImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var trimmed = relativePath.TrimStart('~').TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_env.WebRootPath, trimmed);

        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}
