using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Services;

public class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _context;
    private readonly IBudgetService _budgetService;

    public TransactionService(ApplicationDbContext context, IBudgetService budgetService)
    {
        _context = context;
        _budgetService = budgetService;
    }

    public async Task<(decimal TotalIncome, decimal TotalExpense, decimal Balance)> GetBalanceAsync(int userId)
    {
        var totals = await _context.Transactions
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        decimal totalIncome = totals.FirstOrDefault(t => t.Type == "Income")?.Total ?? 0;
        decimal totalExpense = totals.FirstOrDefault(t => t.Type == "Expense")?.Total ?? 0;
        decimal balance = totalIncome - totalExpense;

        return (totalIncome, totalExpense, balance);
    }

    public async Task<(List<Transaction> Transactions, int TotalCount)> GetTransactionsAsync(
        int userId, int? month, int? year, string? type, int? categoryId, string? keyword, int page, int pageSize)
    {
        var query = _context.Transactions
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

        return (transactions, totalCount);
    }

    public async Task<Transaction?> GetTransactionByIdAsync(int id, int userId)
    {
        return await _context.Transactions
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);
    }

    public async Task<(bool Success, string Message, Transaction? Transaction)> CreateTransactionAsync(
        Transaction transaction, IFormFile? invoiceFile, string uploadsFolder)
    {
        // 1. Kiểm tra số tiền
        if (transaction.Amount <= 0)
        {
            return (false, "Số tiền giao dịch phải lớn hơn 0.", null);
        }

        // 2. Kiểm tra ngày giao dịch không được sau ngày hiện tại
        if (transaction.TransactionDate.Date > DateTime.Today)
        {
            return (false, "Ngày giao dịch không được sau ngày hiện tại.", null);
        }

        // 3. Kiểm tra danh mục
        var category = await _context.Categories.FindAsync(transaction.CategoryId);
        if (category == null || (!category.IsDefault && category.UserId != transaction.UserId))
        {
            return (false, "Danh mục không tồn tại hoặc bạn không có quyền sử dụng.", null);
        }

        if (!category.IsActive)
        {
            return (false, "Danh mục này đã bị ngừng sử dụng.", null);
        }

        // 4. Kiểm tra loại giao dịch phải khớp loại danh mục
        if (category.Type != transaction.Type)
        {
            return (false, $"Loại giao dịch ({transaction.Type}) không khớp với loại của danh mục ({category.Type}).", null);
        }

        // 5. Xử lý upload ảnh hóa đơn
        if (invoiceFile != null && invoiceFile.Length > 0)
        {
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(invoiceFile.FileName).ToLower();

            if (!allowedExts.Contains(ext))
            {
                return (false, "Định dạng ảnh hóa đơn không hợp lệ (.jpg, .jpeg, .png, .webp).", null);
            }

            if (invoiceFile.Length > 5 * 1024 * 1024)
            {
                return (false, "Dung lượng ảnh hóa đơn không được vượt quá 5MB.", null);
            }

            var invoicesDir = Path.Combine(uploadsFolder, "invoices");
            if (!Directory.Exists(invoicesDir))
            {
                Directory.CreateDirectory(invoicesDir);
            }

            var uniqueName = $"inv_{transaction.UserId}_{Guid.NewGuid():N}{ext}";
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

        // 6. Tự động kiểm tra ngân sách nếu là giao dịch chi tiêu (Expense)
        if (transaction.Type == "Expense")
        {
            await _budgetService.CheckAndTriggerBudgetAlertAsync(
                transaction.UserId, transaction.CategoryId, transaction.TransactionDate);
        }

        return (true, "Thêm giao dịch thành công!", transaction);
    }

    public async Task<(bool Success, string Message)> UpdateTransactionAsync(
        Transaction transaction, IFormFile? invoiceFile, string uploadsFolder, int userId)
    {
        var existing = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == transaction.TransactionId && t.UserId == userId);
        if (existing == null)
        {
            return (false, "Không tìm thấy giao dịch hoặc bạn không có quyền sửa.");
        }

        if (transaction.Amount <= 0)
        {
            return (false, "Số tiền giao dịch phải lớn hơn 0.");
        }

        if (transaction.TransactionDate.Date > DateTime.Today)
        {
            return (false, "Ngày giao dịch không được sau ngày hiện tại.");
        }

        var category = await _context.Categories.FindAsync(transaction.CategoryId);
        if (category == null || (!category.IsDefault && category.UserId != userId))
        {
            return (false, "Danh mục không hợp lệ.");
        }

        if (category.Type != transaction.Type)
        {
            return (false, "Loại giao dịch không khớp với danh mục đã chọn.");
        }

        int oldCategoryId = existing.CategoryId;
        DateTime oldDate = existing.TransactionDate;

        existing.CategoryId = transaction.CategoryId;
        existing.Type = transaction.Type;
        existing.Amount = transaction.Amount;
        existing.TransactionDate = transaction.TransactionDate;
        existing.Note = transaction.Note;
        existing.UpdatedAt = DateTime.UtcNow;

        // Xử lý thay đổi ảnh hóa đơn
        if (invoiceFile != null && invoiceFile.Length > 0)
        {
            var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(invoiceFile.FileName).ToLower();

            if (allowedExts.Contains(ext) && invoiceFile.Length <= 5 * 1024 * 1024)
            {
                var invoicesDir = Path.Combine(uploadsFolder, "invoices");
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

        // Kiểm tra lại ngân sách cho cả danh mục cũ và mới nếu có
        if (existing.Type == "Expense")
        {
            await _budgetService.CheckAndTriggerBudgetAlertAsync(userId, existing.CategoryId, existing.TransactionDate);
            if (oldCategoryId != existing.CategoryId || oldDate.Month != existing.TransactionDate.Month)
            {
                await _budgetService.CheckAndTriggerBudgetAlertAsync(userId, oldCategoryId, oldDate);
            }
        }

        return (true, "Cập nhật giao dịch thành công!");
    }

    public async Task<(bool Success, string Message)> DeleteTransactionAsync(int id, int userId)
    {
        var existing = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionId == id && t.UserId == userId);
        if (existing == null)
        {
            return (false, "Không tìm thấy giao dịch hoặc bạn không có quyền xóa.");
        }

        int catId = existing.CategoryId;
        string type = existing.Type;
        DateTime trxDate = existing.TransactionDate;

        _context.Transactions.Remove(existing);
        await _context.SaveChangesAsync();

        // Kiểm tra lại ngân sách sau khi xóa giao dịch chi
        if (type == "Expense")
        {
            await _budgetService.CheckAndTriggerBudgetAlertAsync(userId, catId, trxDate);
        }

        return (true, "Xóa giao dịch thành công!");
    }
}
