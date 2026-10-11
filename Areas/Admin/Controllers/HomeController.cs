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
        var totalStudents = await _context.Users.CountAsync(u => u.Role == "Student");
        var activeStudents = await _context.Users.CountAsync(u => u.Role == "Student" && u.IsActive);
        var lockedStudents = await _context.Users.CountAsync(u => u.Role == "Student" && !u.IsActive);
        var totalTransactions = await _context.Transactions.CountAsync();
        var defaultCategoriesCount = await _context.Categories.CountAsync(c => c.IsDefault);

        ViewBag.TotalStudents = totalStudents;
        ViewBag.ActiveStudents = activeStudents;
        ViewBag.LockedStudents = lockedStudents;
        ViewBag.TotalTransactions = totalTransactions;
        ViewBag.DefaultCategoriesCount = defaultCategoriesCount;

        // Lấy 5 sinh viên mới đăng ký gần nhất
        var recentStudents = await _context.Users
            .Where(u => u.Role == "Student")
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View(recentStudents);
    }
}
