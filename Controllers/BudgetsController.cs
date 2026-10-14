using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Helpers;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class BudgetsController : Controller
{
    private readonly ApplicationDbContext _context;

    public BudgetsController(ApplicationDbContext context)
    {
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

        var budgetsWithProgress = await BudgetAlertHelper.GetBudgetsWithProgressAsync(
            _context, userId, selectedMonth, selectedYear);

        var expenseCategories = await _context.Categories
            .AsNoTracking()
            .Where(c => (c.IsDefault || c.UserId == userId) && c.Type == "Expense" && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        decimal totalLimit = budgetsWithProgress.Sum(b => b.Budget.LimitAmount);
        decimal totalSpent = budgetsWithProgress.Sum(b => b.SpentAmount);
        decimal totalRemaining = totalLimit - totalSpent;
        double overallPercentage = totalLimit > 0 ? (double)(totalSpent / totalLimit) * 100.0 : 0;

        ViewBag.SelectedMonth = selectedMonth;
        ViewBag.SelectedYear = selectedYear;
        ViewBag.ExpenseCategories = expenseCategories;
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

        var cat = await _context.Categories.FindAsync(budget.CategoryId);
        if (cat == null || cat.Type != "Expense")
        {
            TempData["ErrorMessage"] = "Chỉ có thể thiết lập ngân sách cho các danh mục Chi tiêu (Expense).";
            return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
        }

        if (budget.LimitAmount <= 0)
        {
            TempData["ErrorMessage"] = "Hạn mức ngân sách phải lớn hơn 0.";
            return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
        }

        bool exists = await _context.Budgets.AnyAsync(b => b.UserId == userId && 
                                                           b.CategoryId == budget.CategoryId && 
                                                           b.Month == budget.Month && 
                                                           b.Year == budget.Year);
        if (exists)
        {
            TempData["ErrorMessage"] = $"Đã tồn tại ngân sách cho danh mục này trong tháng {budget.Month}/{budget.Year}.";
            return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
        }

        budget.UserId = userId;
        budget.CreatedAt = DateTime.UtcNow;

        _context.Budgets.Add(budget);
        await _context.SaveChangesAsync();

        await BudgetAlertHelper.CheckAndTriggerBudgetAlertAsync(
            _context, userId, budget.CategoryId, new DateTime(budget.Year, budget.Month, 1));

        TempData["SuccessMessage"] = "Thiết lập ngân sách mới thành công!";
        return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Budget budget)
    {
        int userId = GetCurrentUserId();

        var existing = await _context.Budgets
            .FirstOrDefaultAsync(b => b.BudgetId == budget.BudgetId && b.UserId == userId);

        if (existing == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy thông tin ngân sách cần sửa.";
            return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
        }

        if (budget.LimitAmount <= 0)
        {
            TempData["ErrorMessage"] = "Hạn mức ngân sách phải lớn hơn 0.";
            return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
        }

        existing.LimitAmount = budget.LimitAmount;
        existing.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await BudgetAlertHelper.CheckAndTriggerBudgetAlertAsync(
            _context, userId, existing.CategoryId, new DateTime(existing.Year, existing.Month, 1));

        TempData["SuccessMessage"] = "Cập nhật hạn mức ngân sách thành công!";
        return RedirectToAction(nameof(Index), new { month = budget.Month, year = budget.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int month, int year)
    {
        int userId = GetCurrentUserId();

        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.BudgetId == id && b.UserId == userId);

        if (budget == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy ngân sách để xóa.";
            return RedirectToAction(nameof(Index), new { month, year });
        }

        _context.Budgets.Remove(budget);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Xóa ngân sách thành công!";
        return RedirectToAction(nameof(Index), new { month, year });
    }
}
