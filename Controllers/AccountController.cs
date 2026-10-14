using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;
using BTL_LTWEB_QLChiTieu.ViewModels;

namespace BTL_LTWEB_QLChiTieu.Controllers;

public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public AccountController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower().Trim());
        if (emailExists)
        {
            ModelState.AddModelError(string.Empty, "Email này đã được sử dụng bởi một tài khoản khác.");
            return View(model);
        }

        if (!string.IsNullOrWhiteSpace(model.StudentCode))
        {
            var codeExists = await _context.Users.AnyAsync(u => u.StudentCode == model.StudentCode.Trim());
            if (codeExists)
            {
                ModelState.AddModelError(string.Empty, "Mã sinh viên này đã tồn tại trong hệ thống.");
                return View(model);
            }
        }

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

        var newUser = new AppUser
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim().ToLower(),
            PasswordHash = passwordHash,
            StudentCode = model.StudentCode?.Trim(),
            SchoolName = model.SchoolName?.Trim(),
            ClassName = model.ClassName?.Trim(),
            Role = "Student",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
            {
                return RedirectToAction("Index", "Home", new { area = "Admin" });
            }
            return RedirectToAction("Index", "Home");
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var input = model.Email.Trim();
        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == input.ToLower() ||
            (u.StudentCode != null && u.StudentCode == input) ||
            (input.ToLower() == "admin" && u.Role == "Admin") ||
            (input.ToLower() == "student" && u.Role == "Student"));

        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
            return View(model);
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Quản trị viên.");
            return View(model);
        }

        bool isPasswordValid = false;
        try
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
        }
        catch
        {
            isPasswordValid = false;
        }

        if (!isPasswordValid && user.PasswordHash == model.Password)
        {
            isPasswordValid = true;
        }

        if (!isPasswordValid)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("StudentCode", user.StudentCode ?? string.Empty),
            new Claim("AvatarPath", user.AvatarPath ?? "/images/default-avatar.png")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(4)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        HttpContext.Session.SetInt32("UserId", user.UserId);
        HttpContext.Session.SetString("UserRole", user.Role);
        HttpContext.Session.SetString("UserName", user.FullName);

        if (user.Role == "Admin")
        {
            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> SignOutGet()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        int userId = GetCurrentUserId();
        if (userId == 0) return RedirectToAction(nameof(Login));

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        var model = new ProfileViewModel
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            StudentCode = user.StudentCode,
            SchoolName = user.SchoolName,
            ClassName = user.ClassName,
            AvatarPath = user.AvatarPath
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        int userId = GetCurrentUserId();
        if (userId == 0) return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.FullName = model.FullName.Trim();
        if (!string.IsNullOrWhiteSpace(model.StudentCode))
        {
            user.StudentCode = model.StudentCode.Trim();
        }
        user.SchoolName = model.SchoolName?.Trim();
        user.ClassName = model.ClassName?.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(model.AvatarFile.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(string.Empty, "Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp");
                return View(model);
            }

            if (model.AvatarFile.Length > 2 * 1024 * 1024)
            {
                ModelState.AddModelError(string.Empty, "Dung lượng ảnh đại diện không được vượt quá 2MB.");
                return View(model);
            }

            var avatarsDir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            if (!Directory.Exists(avatarsDir))
            {
                Directory.CreateDirectory(avatarsDir);
            }

            var uniqueFileName = $"avatar_{userId}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(avatarsDir, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.AvatarFile.CopyToAsync(stream);
            }

            user.AvatarPath = $"/uploads/avatars/{uniqueFileName}";
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Cập nhật hồ sơ cá nhân thành công!";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
