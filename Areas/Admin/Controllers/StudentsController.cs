using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;

namespace BTL_LTWEB_QLChiTieu.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class StudentsController : Controller
{
    private readonly ApplicationDbContext _context;

    public StudentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? keyword, int page = 1, int pageSize = 10)
    {
        var query = _context.Users.Where(u => u.Role == "Student").AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(kw) ||
                                     u.Email.ToLower().Contains(kw) ||
                                     (u.StudentCode != null && u.StudentCode.ToLower().Contains(kw)));
        }

        var totalCount = await query.CountAsync();
        var students = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Keyword = keyword;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.TotalCount = totalCount;

        return View(students);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var student = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id && u.Role == "Student");
        if (student == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy tài khoản sinh viên.";
            return RedirectToAction(nameof(Index));
        }

        student.IsActive = !student.IsActive;
        student.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = student.IsActive
            ? $"Đã mở khóa tài khoản của sinh viên {student.FullName}."
            : $"Đã khóa tài khoản của sinh viên {student.FullName}. Tài khoản này sẽ không thể đăng nhập.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id)
    {
        var student = await _context.Users
            .Include(u => u.Transactions)
            .Include(u => u.Budgets)
            .Include(u => u.SavingsGoals)
            .FirstOrDefaultAsync(u => u.UserId == id && u.Role == "Student");

        if (student == null)
        {
            return NotFound();
        }

        var totalIncome = student.Transactions.Where(t => t.Type == "Income").Sum(t => t.Amount);
        var totalExpense = student.Transactions.Where(t => t.Type == "Expense").Sum(t => t.Amount);
        var balance = totalIncome - totalExpense;

        ViewBag.TotalIncome = totalIncome;
        ViewBag.TotalExpense = totalExpense;
        ViewBag.Balance = balance;

        return View(student);
    }
}
