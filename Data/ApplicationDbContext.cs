using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<Budget> Budgets { get; set; } = null!;
    public DbSet<SavingsGoal> SavingsGoals { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Cấu hình bảng Users
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => u.StudentCode).IsUnique().HasFilter("[StudentCode] IS NOT NULL");
        });

        // 2. Cấu hình bảng Categories
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasOne(c => c.User)
                  .WithMany(u => u.Categories)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 3. Cấu hình bảng Transactions
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.HasOne(t => t.User)
                  .WithMany(u => u.Transactions)
                  .HasForeignKey(t => t.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.Category)
                  .WithMany(c => c.Transactions)
                  .HasForeignKey(t => t.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(t => new { t.UserId, t.TransactionDate });
            entity.HasIndex(t => t.CategoryId);
        });

        // 4. Cấu hình bảng Budgets
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasOne(b => b.User)
                  .WithMany(u => u.Budgets)
                  .HasForeignKey(b => b.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(b => b.Category)
                  .WithMany(c => c.Budgets)
                  .HasForeignKey(b => b.CategoryId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Mỗi sinh viên chỉ có 1 ngân sách cho 1 danh mục trong 1 tháng
            entity.HasIndex(b => new { b.UserId, b.CategoryId, b.Month, b.Year }).IsUnique();
        });

        // 5. Cấu hình bảng SavingsGoals
        modelBuilder.Entity<SavingsGoal>(entity =>
        {
            entity.HasOne(s => s.User)
                  .WithMany(u => u.SavingsGoals)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. Cấu hình bảng Notifications
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne(n => n.User)
                  .WithMany(u => u.Notifications)
                  .HasForeignKey(n => n.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(n => n.Budget)
                  .WithMany(b => b.Notifications)
                  .HasForeignKey(n => n.BudgetId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Tránh lặp cảnh báo cùng mức cho 1 ngân sách
            entity.HasIndex(n => new { n.BudgetId, n.AlertLevel }).IsUnique();
        });

        // 7. Seed Data khởi tạo danh mục mặc định & tài khoản
        SeedDefaultData(modelBuilder);
    }

    private static void SeedDefaultData(ModelBuilder modelBuilder)
    {
        // Tài khoản mẫu: Admin (Admin@123) & Sinh viên (Student@123)
        var adminCreated = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        modelBuilder.Entity<AppUser>().HasData(
            new AppUser
            {
                UserId = 1,
                FullName = "Quản trị viên Hệ thống",
                Email = "admin@utc.edu.vn",
                PasswordHash = "$2a$11$mR/Vwa8oN7KYKTigTon5wOMNYkGnG5qERsn8LstND9ypF14.rpVo6",
                Role = "Admin",
                IsActive = true,
                CreatedAt = adminCreated
            },
            new AppUser
            {
                UserId = 2,
                FullName = "Trần Nhật Thạch",
                Email = "student@utc.edu.vn",
                PasswordHash = "$2a$11$.MuQ6KnBOmWV.6CT5jj8wORlALZKClrQcM3D97zHVdYCqi7d8bbMG",
                StudentCode = "211200001",
                SchoolName = "Đại học Giao thông Vận tải",
                ClassName = "CNTT K62",
                Role = "Student",
                IsActive = true,
                CreatedAt = adminCreated
            }
        );

        // 11 Danh mục mặc định hệ thống
        modelBuilder.Entity<Category>().HasData(
            // Thu nhập (Income)
            new Category { CategoryId = 1, Name = "Tiền lương, làm thêm", Type = "Income", Icon = "bi-cash-stack", Color = "#198754", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 2, Name = "Chu cấp từ gia đình", Type = "Income", Icon = "bi-house-heart", Color = "#0d6efd", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 3, Name = "Học bổng", Type = "Income", Icon = "bi-mortarboard-fill", Color = "#ffc107", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 4, Name = "Thu nhập khác", Type = "Income", Icon = "bi-wallet2", Color = "#20c997", IsDefault = true, IsActive = true, CreatedAt = adminCreated },

            // Chi tiêu (Expense)
            new Category { CategoryId = 5, Name = "Ăn uống", Type = "Expense", Icon = "bi-cup-hot-fill", Color = "#fd7e14", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 6, Name = "Nhà trọ", Type = "Expense", Icon = "bi-building", Color = "#6610f2", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 7, Name = "Đi lại", Type = "Expense", Icon = "bi-bus-front-fill", Color = "#0dcaf0", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 8, Name = "Học tập", Type = "Expense", Icon = "bi-book-half", Color = "#0d6efd", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 9, Name = "Mua sắm", Type = "Expense", Icon = "bi-bag-heart-fill", Color = "#d63384", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 10, Name = "Giải trí", Type = "Expense", Icon = "bi-controller", Color = "#6f42c1", IsDefault = true, IsActive = true, CreatedAt = adminCreated },
            new Category { CategoryId = 11, Name = "Chi phí khác", Type = "Expense", Icon = "bi-tags-fill", Color = "#6c757d", IsDefault = true, IsActive = true, CreatedAt = adminCreated }
        );
    }
}
