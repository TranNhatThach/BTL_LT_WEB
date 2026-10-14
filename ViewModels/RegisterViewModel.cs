using System.ComponentModel.DataAnnotations;

namespace BTL_LTWEB_QLChiTieu.ViewModels;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [Display(Name = "Họ và tên")]
    [StringLength(150, ErrorMessage = "Họ và tên không vượt quá 150 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải từ 6 ký tự trở lên")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Nhập lại mật khẩu")]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không trùng khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? StudentCode { get; set; }
    public string? SchoolName { get; set; }
    public string? ClassName { get; set; }
}
