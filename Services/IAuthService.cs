using BTL_LTWEB_QLChiTieu.Models;
using BTL_LTWEB_QLChiTieu.ViewModels;

namespace BTL_LTWEB_QLChiTieu.Services;

public interface IAuthService
{
    Task<(bool Success, string Message, AppUser? User)> RegisterAsync(RegisterViewModel model);
    Task<(bool Success, string Message, AppUser? User)> LoginAsync(LoginViewModel model);
    Task<AppUser?> GetUserByIdAsync(int userId);
    Task<(bool Success, string Message)> UpdateProfileAsync(int userId, ProfileViewModel model, string uploadsFolder);
}
