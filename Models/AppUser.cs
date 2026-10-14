using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BTL_LTWEB_QLChiTieu.Models;

[Table("Users")]
public class AppUser
{
    [Key]
    public int UserId { get; set; }

    [Required]
    [StringLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [StringLength(50)]
    public string? StudentCode { get; set; }

    [StringLength(200)]
    public string? SchoolName { get; set; }

    [StringLength(100)]
    public string? ClassName { get; set; }

    [StringLength(500)]
    public string? AvatarPath { get; set; }

    [Required]
    [StringLength(20)]
    public string Role { get; set; } = "Student"; // "Student" hoặc "Admin"

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Quan hệ
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public virtual ICollection<Budget> Budgets { get; set; } = new List<Budget>();
    public virtual ICollection<SavingsGoal> SavingsGoals { get; set; } = new List<SavingsGoal>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<FinancialNote> FinancialNotes { get; set; } = new List<FinancialNote>();
}
