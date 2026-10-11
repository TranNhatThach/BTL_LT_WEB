using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTL_LTWEB_QLChiTieu.Models;

[Table("Categories")]
public class Category
{
    [Key]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên danh mục")]
    [StringLength(100, ErrorMessage = "Tên danh mục không quá 100 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string Type { get; set; } = "Expense"; // "Income" hoặc "Expense"

    [StringLength(100)]
    public string? Icon { get; set; } // Bootstrap Icons, ví dụ: 'bi-cup-hot-fill'

    [StringLength(20)]
    public string? Color { get; set; } // Mã màu Hex, ví dụ: '#fd7e14'

    public int? UserId { get; set; } // NULL: Danh mục hệ thống mặc định; Có giá trị: Danh mục cá nhân

    public bool IsDefault { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Quan hệ
    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public virtual ICollection<Budget> Budgets { get; set; } = new List<Budget>();
}
