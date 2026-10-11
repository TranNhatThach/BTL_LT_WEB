using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace BTL_LTWEB_QLChiTieu.ViewModels;

public class ProfileViewModel
{
    public int UserId { get; set; }

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [Display(Name = "Họ và tên")]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Mã sinh viên")]
    public string? StudentCode { get; set; }

    [Display(Name = "Trường học")]
    [StringLength(200)]
    public string? SchoolName { get; set; }

    [Display(Name = "Lớp")]
    [StringLength(100)]
    public string? ClassName { get; set; }

    [Display(Name = "Ảnh đại diện hiện tại")]
    public string? AvatarPath { get; set; }

    [Display(Name = "Tải lên ảnh mới")]
    public IFormFile? AvatarFile { get; set; }
}
