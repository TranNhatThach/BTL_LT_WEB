using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Services;

namespace BTL_LTWEB_QLChiTieu.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBudgetService _budgetService;

    public HomeController(ApplicationDbContext context, IBudgetService budgetService)
    {
        _context = context;
        _budgetService = budgetService;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    public async Task<IActionResult> Index()
    {
        // 1. Chưa đăng nhập thì chuyển hướng sang Login
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("Login", "Account");
        }

        // 2. Nếu là Admin thì chuyển hướng sang Admin Dashboard
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }

        int userId = GetCurrentUserId();
        int currentMonth = DateTime.Today.Month;
        int currentYear = DateTime.Today.Year;
        int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

        // 3. Tính toán 4 thẻ thống kê
        var allTimeTotals = await _context.Transactions
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        decimal totalAllIncome = allTimeTotals.FirstOrDefault(t => t.Type == "Income")?.Total ?? 0;
        decimal totalAllExpense = allTimeTotals.FirstOrDefault(t => t.Type == "Expense")?.Total ?? 0;
        decimal currentBalance = totalAllIncome - totalAllExpense;

        var currentMonthTrx = await _context.Transactions
            .Where(t => t.UserId == userId && 
                        t.TransactionDate.Month == currentMonth && 
                        t.TransactionDate.Year == currentYear)
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

        // 4. Dữ liệu Biểu đồ Doughnut (Cơ cấu chi tiêu tháng hiện tại)
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

        // 5. Dữ liệu Biểu đồ Bar (So sánh Thu - Chi 6 tháng gần nhất)
        var last6MonthsLabels = new List<string>();
        var last6MonthsIncome = new List<decimal>();
        var last6MonthsExpense = new List<decimal>();

        for (int i = 5; i >= 0; i--)
        {
            var date = DateTime.Today.AddMonths(-i);
            int m = date.Month;
            int y = date.Year;

            last6MonthsLabels.Add($"T{m}/{y}");

            var monthTotals = await _context.Transactions
                .Where(t => t.UserId == userId && t.TransactionDate.Month == m && t.TransactionDate.Year == y)
                .GroupBy(t => t.Type)
                .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
                .ToListAsync();

            decimal inc = monthTotals.FirstOrDefault(t => t.Type == "Income")?.Total ?? 0;
            decimal exp = monthTotals.FirstOrDefault(t => t.Type == "Expense")?.Total ?? 0;

            last6MonthsIncome.Add(inc);
            last6MonthsExpense.Add(exp);
        }

        ViewBag.BarLabels = System.Text.Json.JsonSerializer.Serialize(last6MonthsLabels);
        ViewBag.BarIncome = System.Text.Json.JsonSerializer.Serialize(last6MonthsIncome);
        ViewBag.BarExpense = System.Text.Json.JsonSerializer.Serialize(last6MonthsExpense);

        // 6. 5 Giao dịch gần đây nhất
        var recentTransactions = await _context.Transactions
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync();

        // 7. Tiến độ ngân sách tháng hiện tại
        var budgets = await _budgetService.GetBudgetsWithProgressAsync(userId, currentMonth, currentYear);
        ViewBag.Budgets = budgets;

        return View(recentTransactions);
    }
}
