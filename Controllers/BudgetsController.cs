using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;
using BTL_LTWEB_QLChiTieu.Services;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class BudgetsController : Controller
{
    private readonly IBudgetService _budgetService;
    private readonly ApplicationDbContext _context;

    public BudgetsController(IBudgetService budgetService, ApplicationDbContext context)
    {
        _budgetService = budgetService;
        _context = context;
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

        var budgetsWithProgress = await _budgetService.GetBudgetsWithProgressAsync(userId, selectedMonth, selectedYear);

        // Lấy danh sách danh mục chi tiêu khả dụng để người dùng chọn thiết lập
        var expenseCategories = await _context.Categories
            .Where(c => (c.IsDefault || c.UserId == userId) && c.Type == "Expense" && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.SelectedMonth = selectedMonth;
        ViewBag.SelectedYear = selectedYear;
        ViewBag.ExpenseCategories = expenseCategories;

        // Tính tổng quan ngân sách tháng
        decimal totalLimit = budgetsWithProgress.Sum(b => b.Budget.LimitAmount);
        decimal totalSpent = budgetsWithProgress.Sum(b => b.SpentAmount);
        decimal totalRemaining = totalLimit - totalSpent;
        double overallPercentage = totalLimit > 0 ? (double)(totalSpent / totalLimit) * 100.0 : 0;

        ViewBag.TotalLimit = totalLimit;
        ViewBag.TotalSpent = totalSpent;
        ViewBag.TotalRemaining = totalRemaining;
        ViewBag.OverallPercentage = overallPercentage;

        return View(budgetsWithProgress);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Budget budget)
    {
        int userId = GetCurrentUserId();

        var result = await _budgetService.CreateBudgetAsync(budget, userId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Budget budget)
    {
        int userId = GetCurrentUserId();

        var result = await _budgetService.UpdateBudgetAsync(budget, userId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int month, int year)
    {
        int userId = GetCurrentUserId();

        var result = await _budgetService.DeleteBudgetAsync(id, userId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index), new { month, year });
    }
}
