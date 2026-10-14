using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;

namespace BTL_LTWEB_QLChiTieu.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var studentStats = await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == "Student")
            .GroupBy(u => u.IsActive)
            .Select(g => new { IsActive = g.Key, Count = g.Count() })
            .ToListAsync();

        int activeStudents = studentStats.FirstOrDefault(s => s.IsActive)?.Count ?? 0;
        int lockedStudents = studentStats.FirstOrDefault(s => !s.IsActive)?.Count ?? 0;
        int totalStudents = activeStudents + lockedStudents;

        var totalTransactions = await _context.Transactions.AsNoTracking().CountAsync();
        var defaultCategoriesCount = await _context.Categories.AsNoTracking().CountAsync(c => c.IsDefault);

        ViewBag.TotalStudents = totalStudents;
        ViewBag.ActiveStudents = activeStudents;
        ViewBag.LockedStudents = lockedStudents;
        ViewBag.TotalTransactions = totalTransactions;
        ViewBag.DefaultCategoriesCount = defaultCategoriesCount;

        // Lấy 5 sinh viên mới đăng ký gần nhất (No-Tracking)
        var recentStudents = await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == "Student")
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View(recentStudents);
    }
}
