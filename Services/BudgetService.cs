using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Services;

public class BudgetService : IBudgetService
{
    private readonly ApplicationDbContext _context;

    public BudgetService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task CheckAndTriggerBudgetAlertAsync(int userId, int categoryId, DateTime transactionDate)
    {
        int month = transactionDate.Month;
        int year = transactionDate.Year;

        // Tìm ngân sách của sinh viên cho danh mục này trong tháng
        var budget = await _context.Budgets
            .Include(b => b.Category)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId && b.Month == month && b.Year == year);

        if (budget == null) return;

        // Tính tổng chi thực tế của danh mục trong tháng
        var totalSpent = await _context.Transactions
            .Where(t => t.UserId == userId && 
                        t.CategoryId == categoryId && 
                        t.Type == "Expense" && 
                        t.TransactionDate.Month == month && 
                        t.TransactionDate.Year == year)
            .SumAsync(t => t.Amount);

        double percentage = (double)(totalSpent / budget.LimitAmount) * 100.0;
        string categoryName = budget.Category?.Name ?? "Chi tiêu";

        if (percentage >= 100.0)
        {
            // Kiểm tra xem đã có thông báo mức Exceeded chưa (chống spam)
            bool hasExceededNotif = await _context.Notifications
                .AnyAsync(n => n.BudgetId == budget.BudgetId && n.AlertLevel == "Exceeded");

            if (!hasExceededNotif)
            {
                var notif = new Notification
                {
                    UserId = userId,
                    BudgetId = budget.BudgetId,
                    AlertLevel = "Exceeded",
                    Title = $"Vượt ngân sách: {categoryName}",
                    Message = $"Bạn đã chi vượt hạn mức ngân sách {categoryName} tháng {month}/{year}! Thực chi: {totalSpent:N0} đ / Hạn mức: {budget.LimitAmount:N0} đ ({percentage:F1}%).",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notif);
                await _context.SaveChangesAsync();
            }
        }
        else if (percentage >= 80.0)
        {
            // Kiểm tra xem đã có thông báo mức Warning chưa
            bool hasWarningNotif = await _context.Notifications
                .AnyAsync(n => n.BudgetId == budget.BudgetId && n.AlertLevel == "Warning");

            if (!hasWarningNotif)
            {
                decimal remaining = budget.LimitAmount - totalSpent;
                var notif = new Notification
                {
                    UserId = userId,
                    BudgetId = budget.BudgetId,
                    AlertLevel = "Warning",
                    Title = $"Cảnh báo ngân sách: {categoryName}",
                    Message = $"Bạn đã sử dụng {percentage:F1}% ngân sách {categoryName} tháng {month}/{year}. Số tiền còn lại là {remaining:N0} đ.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Notifications.Add(notif);
                await _context.SaveChangesAsync();
            }
        }
    }

    public async Task EnsureBudgetsInheritedAsync(int userId, int targetMonth, int targetYear)
    {
        // Kiểm tra xem tháng đích đã có bất kỳ ngân sách nào chưa
        bool hasBudgets = await _context.Budgets
            .AnyAsync(b => b.UserId == userId && b.Month == targetMonth && b.Year == targetYear);

        if (hasBudgets) return;

        // Xác định tháng trước liền kề
        int prevMonth = targetMonth - 1;
        int prevYear = targetYear;
        if (prevMonth < 1)
        {
            prevMonth = 12;
            prevYear--;
        }

        // Lấy danh sách ngân sách tháng trước
        var prevBudgets = await _context.Budgets
            .Where(b => b.UserId == userId && b.Month == prevMonth && b.Year == prevYear)
            .ToListAsync();

        if (prevBudgets.Any())
        {
            var newBudgets = prevBudgets.Select(b => new Budget
            {
                UserId = userId,
                CategoryId = b.CategoryId,
                Month = targetMonth,
                Year = targetYear,
                LimitAmount = b.LimitAmount,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Budgets.AddRange(newBudgets);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<List<BudgetProgressViewModel>> GetBudgetsWithProgressAsync(int userId, int month, int year)
    {
        // Tự động kế thừa ngân sách nếu tháng mới chưa có
        await EnsureBudgetsInheritedAsync(userId, month, year);

        var budgets = await _context.Budgets
            .Include(b => b.Category)
            .Where(b => b.UserId == userId && b.Month == month && b.Year == year)
            .OrderBy(b => b.Category!.Name)
            .ToListAsync();

        // Lấy các giao dịch chi trong tháng của user
        var monthlyExpenses = await _context.Transactions
            .Where(t => t.UserId == userId && 
                        t.Type == "Expense" && 
                        t.TransactionDate.Month == month && 
                        t.TransactionDate.Year == year)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Total);

        var result = new List<BudgetProgressViewModel>();
        foreach (var b in budgets)
        {
            decimal spent = monthlyExpenses.TryGetValue(b.CategoryId, out var val) ? val : 0;
            decimal remaining = b.LimitAmount - spent;
            double percent = b.LimitAmount > 0 ? (double)(spent / b.LimitAmount) * 100.0 : 0;

            string status = "Normal";
            if (percent >= 100.0) status = "Exceeded";
            else if (percent >= 80.0) status = "Warning";

            result.Add(new BudgetProgressViewModel
            {
                Budget = b,
                SpentAmount = spent,
                RemainingAmount = remaining,
                Percentage = percent,
                StatusLevel = status
            });
        }

        return result;
    }

    public async Task<(bool Success, string Message)> CreateBudgetAsync(Budget budget, int userId)
    {
        // Kiểm tra danh mục phải là Expense
        var cat = await _context.Categories.FindAsync(budget.CategoryId);
        if (cat == null || cat.Type != "Expense")
        {
            return (false, "Chỉ có thể thiết lập ngân sách cho các danh mục chi tiêu (Expense).");
        }

        // Kiểm tra trùng lặp (1 SV chỉ có 1 ngân sách cho 1 danh mục trong 1 tháng)
        bool exists = await _context.Budgets.AnyAsync(b => b.UserId == userId && 
                                                           b.CategoryId == budget.CategoryId && 
                                                           b.Month == budget.Month && 
                                                           b.Year == budget.Year);
        if (exists)
        {
            return (false, $"Đã tồn tại ngân sách cho danh mục này trong tháng {budget.Month}/{budget.Year}.");
        }

        budget.UserId = userId;
        budget.CreatedAt = DateTime.UtcNow;

        _context.Budgets.Add(budget);
        await _context.SaveChangesAsync();

        // Kiểm tra ngay cảnh báo nếu đã có chi tiêu từ trước trong tháng
        await CheckAndTriggerBudgetAlertAsync(userId, budget.CategoryId, new DateTime(budget.Year, budget.Month, 1));

        return (true, "Thiết lập ngân sách thành công!");
    }

    public async Task<(bool Success, string Message)> UpdateBudgetAsync(Budget budget, int userId)
    {
        var existing = await _context.Budgets.FirstOrDefaultAsync(b => b.BudgetId == budget.BudgetId && b.UserId == userId);
        if (existing == null)
        {
            return (false, "Không tìm thấy thông tin ngân sách hoặc bạn không có quyền sửa.");
        }

        if (budget.LimitAmount <= 0)
        {
            return (false, "Hạn mức ngân sách phải lớn hơn 0.");
        }

        existing.LimitAmount = budget.LimitAmount;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Kiểm tra lại cảnh báo sau khi đổi hạn mức
        await CheckAndTriggerBudgetAlertAsync(userId, existing.CategoryId, new DateTime(existing.Year, existing.Month, 1));

        return (true, "Cập nhật hạn mức ngân sách thành công!");
    }

    public async Task<(bool Success, string Message)> DeleteBudgetAsync(int id, int userId)
    {
        var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);
        if (budget == null)
        {
            return (false, "Không tìm thấy thông tin ngân sách hoặc bạn không có quyền xóa.");
        }

        _context.Budgets.Remove(budget);
        await _context.SaveChangesAsync();

        return (true, "Xóa ngân sách thành công!");
    }
}
