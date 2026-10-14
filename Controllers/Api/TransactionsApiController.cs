using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;

namespace BTL_LTWEB_QLChiTieu.Controllers.Api;

/// <summary>
/// RESTful Web API cung cấp dữ liệu giao dịch, số dư và danh mục dưới định dạng JSON cho ứng dụng ngoài hoặc kiểm thử
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class TransactionsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TransactionsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    private int GetCurrentUserId()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idStr, out int id) ? id : 0;
    }

    /// <summary>
    /// Lấy danh sách giao dịch có hỗ trợ bộ lọc và phân trang
    /// GET /api/transactions?month=10&year=2026&type=Expense&categoryId=5&page=1&pageSize=10
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int? month,
        [FromQuery] int? year,
        [FromQuery] string? type,
        [FromQuery] int? categoryId,
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 10;

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

        var data = transactions.Select(t => new
        {
            t.TransactionId,
            t.Type,
            t.Amount,
            TransactionDate = t.TransactionDate.ToString("yyyy-MM-dd"),
            t.Note,
            t.InvoiceImagePath,
            Category = new
            {
                t.Category?.CategoryId,
                t.Category?.Name,
                t.Category?.Type,
                t.Category?.Icon,
                t.Category?.Color
            }
        });

        return Ok(new
        {
            success = true,
            totalCount,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            data
        });
    }

    /// <summary>
    /// Lấy tổng thu, tổng chi và số dư hiện tại của tài khoản
    /// GET /api/transactions/balance
    /// </summary>
    [HttpGet("transactions/balance")]
    public async Task<IActionResult> GetBalance()
    {
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

        var totals = await _context.Transactions
            .Where(t => t.UserId == userId)
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        decimal totalIncome = totals.FirstOrDefault(t => t.Type == "Income")?.Total ?? 0;
        decimal totalExpense = totals.FirstOrDefault(t => t.Type == "Expense")?.Total ?? 0;
        decimal balance = totalIncome - totalExpense;

        return Ok(new
        {
            totalIncome,
            totalExpense,
            balance
        });
    }

    /// <summary>
    /// Lấy danh sách danh mục khả dụng (hệ thống và cá nhân)
    /// GET /api/categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        int userId = GetCurrentUserId();
        if (userId == 0) return Unauthorized(new { message = "Bạn chưa đăng nhập." });

        var categories = await _context.Categories
            .Where(c => (c.IsDefault || c.UserId == userId) && c.IsActive)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .Select(c => new
            {
                c.CategoryId,
                c.Name,
                c.Type,
                c.Icon,
                c.Color,
                c.IsDefault
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            count = categories.Count,
            data = categories
        });
    }
}
