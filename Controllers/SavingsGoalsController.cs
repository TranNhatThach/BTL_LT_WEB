using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class SavingsGoalsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SavingsGoalsController(ApplicationDbContext context)
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
        int userId = GetCurrentUserId();

        var goals = await _context.SavingsGoals
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Deadline)
            .ToListAsync();

        var goalViewModels = goals.Select(g =>
        {
            decimal remainingAmount = Math.Max(0, g.TargetAmount - g.CurrentAmount);
            double percentage = g.TargetAmount > 0 ? (double)(g.CurrentAmount / g.TargetAmount) * 100.0 : 0;
            percentage = Math.Min(100.0, percentage);

            int daysRemaining = (g.Deadline.Date - DateTime.Today).Days;
            decimal dailySavingsNeeded = 0;
            string status = "InProgress";

            if (g.CurrentAmount >= g.TargetAmount)
            {
                status = "Completed";
            }
            else if (daysRemaining <= 0)
            {
                status = "Overdue";
            }
            else
            {
                dailySavingsNeeded = remainingAmount / daysRemaining;
            }

            return new SavingsGoalItemViewModel
            {
                Goal = g,
                RemainingAmount = remainingAmount,
                Percentage = percentage,
                DaysRemaining = daysRemaining,
                DailySavingsNeeded = dailySavingsNeeded,
                Status = status
            };
        }).ToList();

        return View(goalViewModels);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SavingsGoal goal)
    {
        int userId = GetCurrentUserId();

        if (goal.TargetAmount <= 0)
        {
            TempData["ErrorMessage"] = "Số tiền mục tiêu phải lớn hơn 0.";
            return RedirectToAction(nameof(Index));
        }

        if (goal.Deadline.Date <= DateTime.Today)
        {
            TempData["ErrorMessage"] = "Ngày đến hạn hoàn thành phải ở tương lai.";
            return RedirectToAction(nameof(Index));
        }

        goal.UserId = userId;
        goal.CurrentAmount = Math.Max(0, goal.CurrentAmount);
        goal.CreatedAt = DateTime.UtcNow;

        _context.SavingsGoals.Add(goal);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo mục tiêu tiết kiệm: {goal.Name}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(int id, decimal depositAmount)
    {
        int userId = GetCurrentUserId();

        var goal = await _context.SavingsGoals
            .FirstOrDefaultAsync(s => s.GoalId == id && s.UserId == userId);

        if (goal == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy mục tiêu tiết kiệm.";
            return RedirectToAction(nameof(Index));
        }

        if (depositAmount <= 0)
        {
            TempData["ErrorMessage"] = "Số tiền nạp vào quỹ phải lớn hơn 0.";
            return RedirectToAction(nameof(Index));
        }

        goal.CurrentAmount += depositAmount;
        goal.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Đã nạp thêm {depositAmount:N0} đ vào mục tiêu: {goal.Name}!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(SavingsGoal model)
    {
        int userId = GetCurrentUserId();

        var goal = await _context.SavingsGoals
            .FirstOrDefaultAsync(s => s.GoalId == model.GoalId && s.UserId == userId);

        if (goal == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy mục tiêu tiết kiệm.";
            return RedirectToAction(nameof(Index));
        }

        if (model.TargetAmount <= 0)
        {
            TempData["ErrorMessage"] = "Số tiền mục tiêu phải lớn hơn 0.";
            return RedirectToAction(nameof(Index));
        }

        goal.Name = model.Name.Trim();
        goal.TargetAmount = model.TargetAmount;
        goal.CurrentAmount = Math.Max(0, model.CurrentAmount);
        goal.Deadline = model.Deadline;
        goal.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Cập nhật mục tiêu tiết kiệm thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        int userId = GetCurrentUserId();

        var goal = await _context.SavingsGoals
            .FirstOrDefaultAsync(s => s.GoalId == id && s.UserId == userId);

        if (goal == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy mục tiêu tiết kiệm.";
            return RedirectToAction(nameof(Index));
        }

        _context.SavingsGoals.Remove(goal);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa mục tiêu tiết kiệm thành công.";
        return RedirectToAction(nameof(Index));
    }
}

public class SavingsGoalItemViewModel
{
    public SavingsGoal Goal { get; set; } = null!;
    public decimal RemainingAmount { get; set; }
    public double Percentage { get; set; }
    public int DaysRemaining { get; set; }
    public decimal DailySavingsNeeded { get; set; }
    public string Status { get; set; } = "InProgress"; // "InProgress", "Completed", "Overdue"
}
