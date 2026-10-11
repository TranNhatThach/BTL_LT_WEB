using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTL_LTWEB_QLChiTieu.Models;

[Table("Transactions")]
public class Transaction
{
    [Key]
    public int TransactionId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn danh mục")]
    public int CategoryId { get; set; }

    [Required]
    [StringLength(20)]
    public string Type { get; set; } = "Expense"; // "Income" hoặc "Expense"

    [Required(ErrorMessage = "Vui lòng nhập số tiền")]
    [Range(1000, 100000000000, ErrorMessage = "Số tiền phải lớn hơn 0")]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày giao dịch")]
    [DataType(DataType.Date)]
    public DateTime TransactionDate { get; set; } = DateTime.Today;

    public string? Note { get; set; }

    [StringLength(500)]
    public string? InvoiceImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Quan hệ
    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category? Category { get; set; }
}
