using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;
using BTL_LTWEB_QLChiTieu.ViewModels;

namespace BTL_LTWEB_QLChiTieu.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;

    public AuthService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message, AppUser? User)> RegisterAsync(RegisterViewModel model)
    {
        // 1. Kiểm tra email đã tồn tại chưa
        var emailExists = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.ToLower());
        if (emailExists)
        {
            return (false, "Email này đã được sử dụng bởi một tài khoản khác.", null);
        }

        // 2. Kiểm tra mã sinh viên đã tồn tại chưa
        if (!string.IsNullOrWhiteSpace(model.StudentCode))
        {
            var studentCodeExists = await _context.Users.AnyAsync(u => u.StudentCode == model.StudentCode);
            if (studentCodeExists)
            {
                return (false, "Mã sinh viên này đã được đăng ký trong hệ thống.", null);
            }
        }

        // 3. Băm mật khẩu bằng BCrypt
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);

        // 4. Tạo thực thể người dùng mới (Mặc định là Student)
        var user = new AppUser
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

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return (true, "Đăng ký tài khoản thành công!", user);
    }

    public async Task<(bool Success, string Message, AppUser? User)> LoginAsync(LoginViewModel model)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == model.Email.ToLower());
        if (user == null)
        {
            return (false, "Email hoặc mật khẩu không chính xác.", null);
        }

        if (!user.IsActive)
        {
            return (false, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ Quản trị viên.", null);
        }

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            return (false, "Email hoặc mật khẩu không chính xác.", null);
        }

        return (true, "Đăng nhập thành công!", user);
    }

    public async Task<AppUser?> GetUserByIdAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    public async Task<(bool Success, string Message)> UpdateProfileAsync(int userId, ProfileViewModel model, string uploadsFolder)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return (false, "Không tìm thấy thông tin tài khoản.");
        }

        user.FullName = model.FullName.Trim();
        user.SchoolName = model.SchoolName?.Trim();
        user.ClassName = model.ClassName?.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        // Xử lý upload ảnh đại diện nếu có
        if (model.AvatarFile != null && model.AvatarFile.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(model.AvatarFile.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                return (false, "Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp");
            }

            if (model.AvatarFile.Length > 2 * 1024 * 1024) // 2MB
            {
                return (false, "Dung lượng ảnh đại diện không được vượt quá 2MB.");
            }

            var avatarsDir = Path.Combine(uploadsFolder, "avatars");
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
        return (true, "Cập nhật thông tin hồ sơ thành công!");
    }
}
