using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTL_LTWEB_QLChiTieu.Models;

[Table("Notifications")]
public class Notification
{
    [Key]
    public int NotificationId { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int BudgetId { get; set; }

    [Required]
    [StringLength(20)]
    public string AlertLevel { get; set; } = "Warning"; // "Warning" (80%-99%) hoặc "Exceeded" (>= 100%)

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Quan hệ
    [ForeignKey("UserId")]
    public virtual AppUser? User { get; set; }

    [ForeignKey("BudgetId")]
    public virtual Budget? Budget { get; set; }
}
