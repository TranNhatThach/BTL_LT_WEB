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
    [Display(Name = "Xác nhận mật khẩu")]
    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không trùng khớp")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã sinh viên")]
    [Display(Name = "Mã sinh viên")]
    [StringLength(50)]
    public string StudentCode { get; set; } = string.Empty;

    [Display(Name = "Trường học")]
    [StringLength(200)]
    public string? SchoolName { get; set; }

    [Display(Name = "Lớp")]
    [StringLength(100)]
    public string? ClassName { get; set; }
}
