using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Services;

namespace BTL_LTWEB_QLChiTieu.Controllers.Api;

[ApiController]
[Route("api")]
[Authorize]
public class TransactionsApiController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly ApplicationDbContext _context;

    public TransactionsApiController(ITransactionService transactionService, ApplicationDbContext context)
    {
        _transactionService = transactionService;
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

        var (transactions, totalCount) = await _transactionService.GetTransactionsAsync(
            userId, month, year, type, categoryId, keyword, page, pageSize);

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

        var (totalIncome, totalExpense, balance) = await _transactionService.GetBalanceAsync(userId);

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
