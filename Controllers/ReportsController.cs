using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Helpers;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ExportService _exportService;

    public ReportsController(ApplicationDbContext context, ExportService exportService)
    {
        _context = context;
        _exportService = exportService;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    public async Task<IActionResult> Index(int? month, int? year)
    {
        int userId = GetCurrentUserId();
        int selectedMonth = month ?? DateTime.Today.Month;
        int selectedYear = year ?? DateTime.Today.Year;

        var start = new DateTime(selectedYear, selectedMonth, 1);
        var end = start.AddMonths(1);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.UserId == userId && 
                        t.TransactionDate >= start && 
                        t.TransactionDate < end)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        decimal totalIncome = transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
        decimal totalExpense = transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        decimal netSavings = totalIncome - totalExpense;

        // Cơ cấu chi tiêu theo danh mục
        var expenseCategories = transactions
            .Where(t => t.Type == "Expense")
            .GroupBy(t => t.Category)
            .Select(g => new CategorySummary
            {
                Category = g.Key!,
                Total = g.Sum(x => x.Amount),
                Percentage = totalExpense > 0 ? (double)(g.Sum(x => x.Amount) / totalExpense) * 100.0 : 0
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        // Cơ cấu thu nhập theo danh mục
        var incomeCategories = transactions
            .Where(t => t.Type == "Income")
            .GroupBy(t => t.Category)
            .Select(g => new CategorySummary
            {
                Category = g.Key!,
                Total = g.Sum(x => x.Amount),
                Percentage = totalIncome > 0 ? (double)(g.Sum(x => x.Amount) / totalIncome) * 100.0 : 0
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        ViewBag.SelectedMonth = selectedMonth;
        ViewBag.SelectedYear = selectedYear;
        ViewBag.TotalIncome = totalIncome;
        ViewBag.TotalExpense = totalExpense;
        ViewBag.NetSavings = netSavings;
        ViewBag.ExpenseCategories = expenseCategories;
        ViewBag.IncomeCategories = incomeCategories;

        return View(transactions);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(int month, int year)
    {
        int userId = GetCurrentUserId();

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var transactions = await _context.Transactions
            .Include(t => t.Category)
            .Where(t => t.UserId == userId && 
                        t.TransactionDate.Month == month && 
                        t.TransactionDate.Year == year)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();

        var fileBytes = _exportService.ExportMonthlyReportToExcel(user, month, year, transactions);
        string fileName = $"BaoCaoChiTieu_Thang{month}_{year}.xlsx";

        return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}

public class CategorySummary
{
    public BTL_LTWEB_QLChiTieu.Models.Category Category { get; set; } = null!;
    public decimal Total { get; set; }
    public double Percentage { get; set; }
}
