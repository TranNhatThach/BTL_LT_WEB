using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;
using BTL_LTWEB_QLChiTieu.ViewModels;

namespace BTL_LTWEB_QLChiTieu.Helpers;

/// <summary>
/// Lớp tiện ích hỗ trợ tính toán tiến độ ngân sách và kích hoạt thông báo cảnh báo chi tiêu
/// </summary>
public static class BudgetAlertHelper
{
    /// <summary>
    /// Tự động kế thừa danh sách ngân sách từ tháng trước sang tháng mới nếu tháng mới chưa được thiết lập
    /// </summary>
    public static async Task EnsureBudgetsInheritedAsync(
        ApplicationDbContext context, int userId, int targetMonth, int targetYear)
    {
        // 1. Kiểm tra xem tháng đích đã có bất kỳ ngân sách nào chưa
        bool hasBudgets = await context.Budgets
            .AnyAsync(b => b.UserId == userId && b.Month == targetMonth && b.Year == targetYear);

        if (hasBudgets) return;

        // 2. Xác định tháng trước liền kề
        int prevMonth = targetMonth - 1;
        int prevYear = targetYear;
        if (prevMonth < 1)
        {
            prevMonth = 12;
            prevYear = targetYear - 1;
        }

        // 3. Lấy ngân sách của tháng trước
        var prevBudgets = await context.Budgets
            .Where(b => b.UserId == userId && b.Month == prevMonth && b.Year == prevYear)
            .ToListAsync();

        // 4. Nếu có, sao chép sang tháng đích với cùng hạn mức
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

            context.Budgets.AddRange(newBudgets);
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Lấy danh sách ngân sách kèm tiến độ thực chi và cấp độ cảnh báo trong tháng
    /// </summary>
    public static async Task<List<BudgetProgressViewModel>> GetBudgetsWithProgressAsync(
        ApplicationDbContext context, int userId, int month, int year)
    {
        // Tự động kế thừa ngân sách từ tháng trước nếu tháng này chưa có
        await EnsureBudgetsInheritedAsync(context, userId, month, year);

        // Lấy danh sách ngân sách của sinh viên trong tháng (No-Tracking)
        var budgets = await context.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.UserId == userId && b.Month == month && b.Year == year)
            .OrderBy(b => b.Category!.Name)
            .ToListAsync();

        var startOfMonth = new DateTime(year, month, 1);
        var startOfNextMonth = startOfMonth.AddMonths(1);

        // Lấy tổng các giao dịch chi tiêu trong tháng theo từng danh mục dùng Index khoảng ngày
        var monthlyExpenses = await context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && 
                        t.Type == "Expense" && 
                        t.TransactionDate >= startOfMonth && 
                        t.TransactionDate < startOfNextMonth)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Total);

        var result = new List<BudgetProgressViewModel>();
        foreach (var b in budgets)
        {
            decimal spent = monthlyExpenses.TryGetValue(b.CategoryId, out var val) ? val : 0;
            decimal remaining = b.LimitAmount - spent;
            double percent = b.LimitAmount > 0 ? (double)(spent / b.LimitAmount) * 100.0 : 0;

            // Xác định cấp độ cảnh báo
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

    /// <summary>
    /// Kiểm tra tổng chi tiêu thực tế trong tháng so với hạn mức ngân sách đã đặt.
    /// Nếu đạt từ 80% đến dưới 100%: Tạo thông báo cảnh báo màu vàng (Warning).
    /// Nếu đạt hoặc vượt 100%: Tạo thông báo cảnh báo màu đỏ (Exceeded).
    /// </summary>
    public static async Task CheckAndTriggerBudgetAlertAsync(
        ApplicationDbContext context, int userId, int categoryId, DateTime transactionDate)
    {
        int month = transactionDate.Month;
        int year = transactionDate.Year;

        var budget = await context.Budgets
            .Include(b => b.Category)
            .FirstOrDefaultAsync(b => b.UserId == userId && 
                                      b.CategoryId == categoryId && 
                                      b.Month == month && 
                                      b.Year == year);

        if (budget == null || budget.LimitAmount <= 0) return;

        var totalSpent = await context.Transactions
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
            bool hasExceededNotif = await context.Notifications
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
                context.Notifications.Add(notif);
                await context.SaveChangesAsync();
            }
        }
        else if (percentage >= 80.0)
        {
            bool hasWarningNotif = await context.Notifications
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
                context.Notifications.Add(notif);
                await context.SaveChangesAsync();
            }
        }
    }
}
