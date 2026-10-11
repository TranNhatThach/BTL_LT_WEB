using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;

    public CategoriesController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _context.Categories
            .Where(c => c.IsDefault)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return View(categories);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (string.IsNullOrWhiteSpace(category.Name))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập tên danh mục.";
            return RedirectToAction(nameof(Index));
        }

        category.IsDefault = true;
        category.UserId = null; // Danh mục hệ thống
        category.IsActive = true;
        category.CreatedAt = DateTime.UtcNow;

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã thêm danh mục hệ thống: {category.Name}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Category model)
    {
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == model.CategoryId && c.IsDefault);
        if (category == null)
        {
            return NotFound();
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
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == id && c.IsDefault);
        if (category == null)
        {
            return NotFound();
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
        var category = await _context.Categories.FirstOrDefaultAsync(c => c.CategoryId == id && c.IsDefault);
        if (category == null)
        {
            return NotFound();
        }

        // Kiểm tra xem danh mục có đang được sử dụng trong giao dịch hoặc ngân sách không
        bool isUsedInTransactions = await _context.Transactions.AnyAsync(t => t.CategoryId == id);
        bool isUsedInBudgets = await _context.Budgets.AnyAsync(b => b.CategoryId == id);

        if (isUsedInTransactions || isUsedInBudgets)
        {
            TempData["ErrorMessage"] = "Không thể xóa danh mục này vì đã có dữ liệu giao dịch hoặc ngân sách liên kết! Hãy chuyển sang trạng thái ngừng sử dụng.";
            return RedirectToAction(nameof(Index));
        }

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã xóa danh mục thành công.";
        return RedirectToAction(nameof(Index));
    }
}
