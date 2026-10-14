using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Helpers;

namespace BTL_LTWEB_QLChiTieu.Controllers;

/// <summary>
/// Controller trang chủ (Dashboard) hiển thị thống kê tài chính, biểu đồ và giao dịch gần đây
/// </summary>
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    public async Task<IActionResult> Index()
    {
        // 1. Chưa đăng nhập thì chuyển hướng sang trang Đăng nhập
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        // 2. Nếu là Quản trị viên (Admin) thì chuyển hướng sang Admin Dashboard
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        int userId = GetCurrentUserId();
        int currentMonth = DateTime.Today.Month;
        int currentYear = DateTime.Today.Year;
        int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

        var firstDayOfMonth = new DateTime(currentYear, currentMonth, 1);
        var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

        // 3. Tính toán 4 thẻ thống kê tổng quan (truy vấn No-Tracking siêu nhanh)
        var allTimeTotals = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        decimal totalAllIncome = allTimeTotals.FirstOrDefault(t => t.Type == "Income")?.Total ?? 0;
        decimal totalAllExpense = allTimeTotals.FirstOrDefault(t => t.Type == "Expense")?.Total ?? 0;
        decimal currentBalance = totalAllIncome - totalAllExpense;

        // Dùng khoảng ngày để tận dụng Index trên (UserId, TransactionDate)
        var currentMonthTrx = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && 
                        t.TransactionDate >= firstDayOfMonth && 
                        t.TransactionDate < firstDayOfNextMonth)
            .ToListAsync();

        decimal currentMonthIncome = currentMonthTrx.Where(t => t.Type == "Income").Sum(t => t.Amount);
        decimal currentMonthExpense = currentMonthTrx.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        decimal dailyAverageExpense = daysInMonth > 0 ? (currentMonthExpense / daysInMonth) : 0;

        ViewBag.CurrentBalance = currentBalance;
        ViewBag.CurrentMonthIncome = currentMonthIncome;
        ViewBag.CurrentMonthExpense = currentMonthExpense;
        ViewBag.DailyAverageExpense = dailyAverageExpense;
        ViewBag.CurrentMonth = currentMonth;
        ViewBag.CurrentYear = currentYear;

        // 4. Dữ liệu Biểu đồ Doughnut (Cơ cấu chi tiêu theo danh mục tháng hiện tại)
        var expenseByCategory = currentMonthTrx
            .Where(t => t.Type == "Expense")
            .GroupBy(t => t.CategoryId)
            .Select(g => new
            {
                CategoryId = g.Key,
                Total = g.Sum(x => x.Amount)
            })
            .ToList();

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsDefault || c.UserId == userId)
            .ToDictionaryAsync(c => c.CategoryId, c => c);

        var doughnutLabels = new List<string>();
        var doughnutData = new List<decimal>();
        var doughnutColors = new List<string>();

        foreach (var item in expenseByCategory)
        {
            if (categories.TryGetValue(item.CategoryId, out var cat))
            {
                doughnutLabels.Add(cat.Name);
                doughnutData.Add(item.Total);
                doughnutColors.Add(cat.Color ?? "#6c757d");
            }
        }

        ViewBag.DoughnutLabels = System.Text.Json.JsonSerializer.Serialize(doughnutLabels);
        ViewBag.DoughnutData = System.Text.Json.JsonSerializer.Serialize(doughnutData);
        ViewBag.DoughnutColors = System.Text.Json.JsonSerializer.Serialize(doughnutColors);

        // 5. Dữ liệu Biểu đồ Cột (Tối ưu hóa: 1 câu truy vấn gom nhóm cho toàn bộ 6 tháng thay vì 6 câu truy vấn trong vòng lặp)
        var sixMonthsStart = firstDayOfMonth.AddMonths(-5);
        var sixMonthsAggregates = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.TransactionDate >= sixMonthsStart)
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month, t.Type })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Type, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        var last6MonthsLabels = new List<string>();
        var last6MonthsIncome = new List<decimal>();
        var last6MonthsExpense = new List<decimal>();

        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.Today.AddMonths(-i);
            int m = date.Month;
            int y = date.Year;

            last6MonthsLabels.Add($"T{m}/{y}");

            decimal inc = sixMonthsAggregates.FirstOrDefault(d => d.Year == y && d.Month == m && d.Type == "Income")?.Total ?? 0;
            decimal exp = sixMonthsAggregates.FirstOrDefault(d => d.Year == y && d.Month == m && d.Type == "Expense")?.Total ?? 0;

            last6MonthsIncome.Add(inc);
            last6MonthsExpense.Add(exp);
        }

        ViewBag.BarLabels = System.Text.Json.JsonSerializer.Serialize(last6MonthsLabels);
        ViewBag.BarIncome = System.Text.Json.JsonSerializer.Serialize(last6MonthsIncome);
        ViewBag.BarExpense = System.Text.Json.JsonSerializer.Serialize(last6MonthsExpense);

        // 6. Danh sách 5 giao dịch gần đây nhất (No-Tracking)
        var recentTransactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync();

        // 7. Tiến độ ngân sách tháng hiện tại
        var budgets = await BudgetAlertHelper.GetBudgetsWithProgressAsync(_context, userId, currentMonth, currentYear);
        ViewBag.Budgets = budgets;

        return View(recentTransactions);
    }
}
