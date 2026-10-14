using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTL_LTWEB_QLChiTieu.Models;

[Table("FinancialNotes")]
public class FinancialNote
{
    [Key]
    public int NoteId { get; set; }

    [Required]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề ghi chú")]
    [StringLength(200, ErrorMessage = "Tiêu đề không được vượt quá 200 ký tự")]
    public string Title { get; set; } = string.Empty;

    public string? Content { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? EstimatedAmount { get; set; }

    [StringLength(50)]
    public string NoteType { get; set; } = "General"; // "General" (Ghi nhớ chung), "ExpensePlan" (Dự kiến chi), "IncomePlan" (Dự kiến thu)

    [StringLength(20)]
    public string Priority { get; set; } = "Normal"; // "Low", "Normal", "High"

    public bool IsCompleted { get; set; } = false;

    public DateTime? DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
