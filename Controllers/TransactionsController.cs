using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Helpers;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class TransactionsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public TransactionsController(ApplicationDbContext context, IWebHostEnvironment env)
    {
        _context = context;
        _env = env;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    public async Task<IActionResult> Index(
        int? month, int? year, string? type, int? categoryId, string? keyword, int page = 1, int pageSize = 10)
    {
        int userId = GetCurrentUserId();

        var query = _context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.UserId == userId)
            .AsQueryable();

        if (month.HasValue && month.Value > 0)
        {
            query = query.Where(t => t.TransactionDate.Month == month.Value);
        }

        if (year.HasValue && year.Value > 0)
        {
            query = query.Where(t => t.TransactionDate.Year == year.Value);
        }

        if (!string.IsNullOrWhiteSpace(type) && type != "All")
        {
            query = query.Where(t => t.Type == type);
        }

        if (categoryId.HasValue && categoryId.Value > 0)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            query = query.Where(t => (t.Note != null && t.Note.ToLower().Contains(kw)) ||
                                     t.Category!.Name.ToLower().Contains(kw));
        }

        var totalCount = await query.CountAsync();
        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totals = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        decimal totalIncome = totals.FirstOrDefault(t => t.Type == "Income")?.Total ?? 0;
        decimal totalExpense = totals.FirstOrDefault(t => t.Type == "Expense")?.Total ?? 0;
        decimal balance = totalIncome - totalExpense;

        var categories = await _context.Categories
            .AsNoTracking()
            .Where(c => (c.IsDefault || c.UserId == userId) && c.IsActive)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToListAsync();

        var existingYears = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .Select(t => t.TransactionDate.Year)
            .Distinct()
            .ToListAsync();

        var availableYears = new HashSet<int>(existingYears);
        for (int y = DateTime.Now.Year; y >= DateTime.Now.Year - 5; y--)
        {
            availableYears.Add(y);
        }

        ViewBag.AvailableYears = availableYears.OrderByDescending(y => y).ToList();
        ViewBag.Month = month;
        ViewBag.Year = year;
        ViewBag.Type = type;
        ViewBag.CategoryId = categoryId;
        ViewBag.Keyword = keyword;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalIncome = totalIncome;
        ViewBag.TotalExpense = totalExpense;
        ViewBag.Balance = balance;
        ViewBag.Categories = categories;

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_TransactionListPartial", transactions);
        }

        return View(transactions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Transaction transaction, string? TransactionDateDisplay, IFormFile? invoiceFile)
    {
        int userId = GetCurrentUserId();
        transaction.UserId = userId;

        if (!string.IsNullOrWhiteSpace(TransactionDateDisplay))
        {
            var formats = new[] { "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(TransactionDateDisplay.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                transaction.TransactionDate = parsedDate;
            }
        }

        if (transaction.Amount <= 0)
        {
            TempData["ErrorMessage"] = "Số tiền giao dịch phải lớn hơn 0.";
            return RedirectToAction(nameof(Index));
        }

        if (transaction.TransactionDate.Date > DateTime.Today)
        {
            TempData["ErrorMessage"] = "Ngày giao dịch không được sau ngày hiện tại.";
            return RedirectToAction(nameof(Index));
        }

        var category = await _context.Categories.FindAsync(transaction.CategoryId);
        if (category == null || (!category.IsDefault && category.UserId != userId) || !category.IsActive)
        {
            TempData["ErrorMessage"] = "Danh mục không hợp lệ hoặc đã ngừng hoạt động.";
            return RedirectToAction(nameof(Index));
        }

        if (category.Type != transaction.Type)
        {
            TempData["ErrorMessage"] = "Loại giao dịch không khớp với loại danh mục đã chọn.";
            return RedirectToAction(nameof(Index));
        }

        if (invoiceFile != null && invoiceFile.Length > 0)
        {
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(invoiceFile.FileName).ToLower();

            if (!allowedExts.Contains(ext) || invoiceFile.Length > 5 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "File hóa đơn không hợp lệ (phải là ảnh .jpg, .png và <= 5MB).";
                return RedirectToAction(nameof(Index));
            }

            var invoicesDir = Path.Combine(_env.WebRootPath, "uploads", "invoices");
            if (!Directory.Exists(invoicesDir))
            {
                Directory.CreateDirectory(invoicesDir);
            }

            var uniqueName = $"inv_{userId}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(invoicesDir, uniqueName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await invoiceFile.CopyToAsync(stream);
            }

            transaction.InvoiceImagePath = $"/uploads/invoices/{uniqueName}";
        }

        transaction.CreatedAt = DateTime.UtcNow;
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        if (transaction.Type == "Expense")
        {
            await BudgetAlertHelper.CheckAndTriggerBudgetAlertAsync(
                _context, userId, transaction.CategoryId, transaction.TransactionDate);
        }

        TempData["SuccessMessage"] = "Thêm giao dịch mới thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Transaction transaction, string? TransactionDateDisplay, IFormFile? invoiceFile)
    {
        int userId = GetCurrentUserId();

        if (!string.IsNullOrWhiteSpace(TransactionDateDisplay))
        {
            var formats = new[] { "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "yyyy-MM-dd" };
            if (DateTime.TryParseExact(TransactionDateDisplay.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                transaction.TransactionDate = parsedDate;
            }
        }

        var existing = await _context.Transactions
            .FirstOrDefaultAsync(t => t.TransactionId == transaction.TransactionId && t.UserId == userId);

        if (existing == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy giao dịch hoặc bạn không có quyền sửa.";
            return RedirectToAction(nameof(Index));
        }

        if (transaction.Amount <= 0 || transaction.TransactionDate.Date > DateTime.Today)
        {
            TempData["ErrorMessage"] = "Dữ liệu giao dịch không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var category = await _context.Categories.FindAsync(transaction.CategoryId);
        if (category == null || (!category.IsDefault && category.UserId != userId) || category.Type != transaction.Type)
        {
            TempData["ErrorMessage"] = "Danh mục không hợp lệ hoặc không khớp với loại giao dịch.";
            return RedirectToAction(nameof(Index));
        }

        int oldCategoryId = existing.CategoryId;
        DateTime oldDate = existing.TransactionDate;

        existing.CategoryId = transaction.CategoryId;
        existing.Type = transaction.Type;
        existing.Amount = transaction.Amount;
        existing.TransactionDate = transaction.TransactionDate;
        existing.Note = transaction.Note;
        existing.UpdatedAt = DateTime.UtcNow;

        if (invoiceFile != null && invoiceFile.Length > 0)
        {
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(invoiceFile.FileName).ToLower();

            if (allowedExts.Contains(ext) && invoiceFile.Length <= 5 * 1024 * 1024)
            {
                var invoicesDir = Path.Combine(_env.WebRootPath, "uploads", "invoices");
                if (!Directory.Exists(invoicesDir))
                {
                    Directory.CreateDirectory(invoicesDir);
                }

                var uniqueName = $"inv_{userId}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(invoicesDir, uniqueName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await invoiceFile.CopyToAsync(stream);
                }

                existing.InvoiceImagePath = $"/uploads/invoices/{uniqueName}";
            }
        }

        await _context.SaveChangesAsync();

        if (existing.Type == "Expense")
        {
            await BudgetAlertHelper.CheckAndTriggerBudgetAlertAsync(_context, userId, existing.CategoryId, existing.TransactionDate);
            if (oldCategoryId != existing.CategoryId || oldDate.Month != existing.TransactionDate.Month)
            {
                await BudgetAlertHelper.CheckAndTriggerBudgetAlertAsync(_context, userId, oldCategoryId, oldDate);
            }
        }

        TempData["SuccessMessage"] = "Cập nhật giao dịch thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        int userId = GetCurrentUserId();

        var existing = await _context.Transactions
            .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

        if (existing == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy giao dịch để xóa.";
            return RedirectToAction(nameof(Index));
        }

        int catId = existing.CategoryId;
        string type = existing.Type;
        DateTime trxDate = existing.TransactionDate;

        _context.Transactions.Remove(existing);
        await _context.SaveChangesAsync();

        if (type == "Expense")
        {
            await BudgetAlertHelper.CheckAndTriggerBudgetAlertAsync(_context, userId, catId, trxDate);
        }

        TempData["SuccessMessage"] = "Xóa giao dịch thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        int userId = GetCurrentUserId();
        var trx = await _context.Transactions
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);

        if (trx == null)
        {
            return NotFound();
        }

        return Json(new
        {
            transactionId = trx.TransactionId,
            type = trx.Type,
            categoryId = trx.CategoryId,
            amount = trx.Amount,
            transactionDate = trx.TransactionDate.ToString("yyyy-MM-dd"),
            note = trx.Note,
            invoiceImagePath = trx.InvoiceImagePath
        });
    }
}
