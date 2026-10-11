using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTL_LTWEB_QLChiTieu.Models;

[Table("SavingsGoals")]
public class SavingsGoal
{
    [Key]
    public int GoalId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên mục tiêu")]
    [StringLength(200, ErrorMessage = "Tên mục tiêu không quá 200 ký tự")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số tiền mục tiêu")]
    [Range(1000, 100000000000, ErrorMessage = "Số tiền mục tiêu phải lớn hơn 0")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TargetAmount { get; set; }

    [Range(0, 100000000000, ErrorMessage = "Số tiền hiện có không được âm")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentAmount { get; set; } = 0;

    [Required(ErrorMessage = "Vui lòng chọn ngày đến hạn")]
    [DataType(DataType.Date)]
    public DateTime Deadline { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Quan hệ
    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }
}
