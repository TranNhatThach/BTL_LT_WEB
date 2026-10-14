using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
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

        // Lấy danh mục hệ thống và danh mục cá nhân của sinh viên (No-Tracking)
        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsDefault || c.UserId == userId)
            .OrderBy(c => c.Type)
            .ThenByDescending(c => c.IsDefault)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        int userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(category.Name))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập tên danh mục.";
            return RedirectToAction(nameof(Index));
        }

        category.UserId = userId;
        category.IsDefault = false; // Danh mục cá nhân
        category.IsActive = true;
        category.CreatedAt = DateTime.UtcNow;

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo danh mục cá nhân: {category.Name}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Category model)
    {
        int userId = GetCurrentUserId();

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == model.CategoryId && c.UserId == userId);

        if (category == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy danh mục hoặc bạn không có quyền sửa danh mục này.";
            return RedirectToAction(nameof(Index));
        }

        category.Name = model.Name.Trim();
        category.Icon = model.Icon;
        category.Color = model.Color;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Cập nhật danh mục thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        int userId = GetCurrentUserId();

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == id && c.UserId == userId);

        if (category == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy danh mục cá nhân.";
            return RedirectToAction(nameof(Index));
        }

        category.IsActive = !category.IsActive;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = category.IsActive
            ? $"Đã kích hoạt danh mục {category.Name}."
            : $"Đã ngừng sử dụng danh mục {category.Name}.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        int userId = GetCurrentUserId();

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.CategoryId == id && c.UserId == userId);

        if (category == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy danh mục cá nhân.";
            return RedirectToAction(nameof(Index));
        }

        // Kiểm tra xem danh mục có đang được sử dụng trong giao dịch hoặc ngân sách không
        bool isUsedInTransactions = await _context.Transactions.AnyAsync(t => t.CategoryId == id && t.UserId == userId);
        bool isUsedInBudgets = await _context.Budgets.AnyAsync(b => b.CategoryId == id && b.UserId == userId);

        if (isUsedInTransactions || isUsedInBudgets)
        {
            TempData["ErrorMessage"] = "Không thể xóa danh mục này vì đã có dữ liệu giao dịch hoặc ngân sách liên kết! Bạn có thể chuyển sang trạng thái ngừng dùng.";
            return RedirectToAction(nameof(Index));
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa danh mục cá nhân thành công.";
        return RedirectToAction(nameof(Index));
    }
}
