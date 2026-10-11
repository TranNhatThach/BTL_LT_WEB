using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTL_LTWEB_QLChiTieu.Models;

[Table("Budgets")]
public class Budget
{
    [Key]
    public int BudgetId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn danh mục chi tiêu")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn tháng")]
    [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12")]
    public int Month { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn năm")]
    public int Year { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập hạn mức")]
    [Range(1000, 100000000000, ErrorMessage = "Hạn mức phải lớn hơn 0")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal LimitAmount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Quan hệ
    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category? Category { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
