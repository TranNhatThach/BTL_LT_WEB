using System.ComponentModel.DataAnnotations;

namespace BTL_LTWEB_QLChiTieu.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Email, Mã sinh viên hoặc Tên đăng nhập")]
    [Display(Name = "Email hoặc Mã sinh viên")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
