using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Controllers;

/// <summary>
/// Controller quản lý Sổ tay ghi chú & Kế hoạch tài chính cá nhân
/// </summary>
[Authorize]
public class NotesController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotesController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    // =========================================================================
    // 1. DANH SÁCH GHI CHÚ & KẾ HOẠCH TÀI CHÍNH (INDEX)
    // =========================================================================
    public async Task<IActionResult> Index(string? type, string? status, string? search)
    {
        int userId = GetCurrentUserId();

        var query = _context.FinancialNotes
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .AsQueryable();

        // Lọc theo loại ghi chú (NoteType)
        if (!string.IsNullOrWhiteSpace(type) && type != "All")
        {
            query = query.Where(n => n.NoteType == type);
        }

        // Lọc theo trạng thái hoàn thành (status)
        if (status == "pending")
        {
            query = query.Where(n => !n.IsCompleted);
        }
        else if (status == "completed")
        {
            query = query.Where(n => n.IsCompleted);
        }

        // Tìm kiếm theo từ khóa trong Tiêu đề hoặc Nội dung
        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim().ToLower();
            query = query.Where(n => n.Title.ToLower().Contains(kw) ||
                                     (n.Content != null && n.Content.ToLower().Contains(kw)));
        }

        var notes = await query
            .OrderBy(n => n.IsCompleted)                 // Việc chưa xong lên trước
            .ThenByDescending(n => n.Priority == "High") // Ưu tiên cao lên trước
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();

        // Thống kê nhanh toàn bộ ghi chú của user
        var allUserNotes = await _context.FinancialNotes
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .ToListAsync();

        ViewBag.TotalNotes = allUserNotes.Count;
        ViewBag.PendingCount = allUserNotes.Count(n => !n.IsCompleted);
        ViewBag.CompletedCount = allUserNotes.Count(n => n.IsCompleted);
        ViewBag.TotalPendingExpense = allUserNotes
            .Where(n => !n.IsCompleted && n.NoteType == "ExpensePlan" && n.EstimatedAmount.HasValue)
            .Sum(n => n.EstimatedAmount!.Value);
        ViewBag.TotalPendingIncome = allUserNotes
            .Where(n => !n.IsCompleted && n.NoteType == "IncomePlan" && n.EstimatedAmount.HasValue)
            .Sum(n => n.EstimatedAmount!.Value);

        // Lưu trạng thái bộ lọc vào ViewBag để hiển thị trên UI
        ViewBag.SelectedType = type ?? "All";
        ViewBag.SelectedStatus = status ?? "All";
        ViewBag.SearchKeyword = search;

        return View(notes);
    }

    // =========================================================================
    // 2. TẠO GHI CHÚ MỚI (CREATE)
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FinancialNote note)
    {
        int userId = GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(note.Title))
        {
            TempData["ErrorMessage"] = "Tiêu đề ghi chú không được để trống.";
            return RedirectToAction(nameof(Index));
        }

        note.UserId = userId;
        note.IsCompleted = false;
        note.CreatedAt = DateTime.UtcNow;

        _context.FinancialNotes.Add(note);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã tạo ghi chú mới: \"{note.Title}\"";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================================
    // 3. CHỈNH SỬA GHI CHÚ (EDIT)
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(FinancialNote model)
    {
        int userId = GetCurrentUserId();

        var note = await _context.FinancialNotes
            .FirstOrDefaultAsync(n => n.NoteId == model.NoteId && n.UserId == userId);

        if (note == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy ghi chú cần chỉnh sửa.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(model.Title))
        {
            TempData["ErrorMessage"] = "Tiêu đề ghi chú không được để trống.";
            return RedirectToAction(nameof(Index));
        }

        note.Title = model.Title.Trim();
        note.Content = model.Content;
        note.EstimatedAmount = model.EstimatedAmount;
        note.NoteType = model.NoteType;
        note.Priority = model.Priority;
        note.DueDate = model.DueDate;
        note.IsCompleted = model.IsCompleted;
        note.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã cập nhật ghi chú \"{note.Title}\"";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================================
    // 4. BẬT / TẮT TRẠNG THÁI HOÀN THÀNH (TOGGLE STATUS)
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        int userId = GetCurrentUserId();

        var note = await _context.FinancialNotes
            .FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == userId);

        if (note == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy ghi chú.";
            return RedirectToAction(nameof(Index));
        }

        note.IsCompleted = !note.IsCompleted;
        note.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = note.IsCompleted
            ? $"Đã đánh dấu hoàn thành: \"{note.Title}\" 🎉"
            : $"Đã chuyển về chưa hoàn thành: \"{note.Title}\"";

        return RedirectToAction(nameof(Index));
    }

    // =========================================================================
    // 5. XÓA GHI CHÚ (DELETE)
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        int userId = GetCurrentUserId();

        var note = await _context.FinancialNotes
            .FirstOrDefaultAsync(n => n.NoteId == id && n.UserId == userId);

        if (note == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy ghi chú cần xóa.";
            return RedirectToAction(nameof(Index));
        }

        _context.FinancialNotes.Remove(note);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Đã xóa ghi chú: \"{note.Title}\"";
        return RedirectToAction(nameof(Index));
    }
}
