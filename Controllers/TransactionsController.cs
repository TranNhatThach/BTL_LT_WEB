using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BTL_LTWEB_QLChiTieu.Data;
using BTL_LTWEB_QLChiTieu.Models;
using BTL_LTWEB_QLChiTieu.Services;

namespace BTL_LTWEB_QLChiTieu.Controllers;

[Authorize]
public class TransactionsController : Controller
{
    private readonly ITransactionService _transactionService;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;

    public TransactionsController(
        ITransactionService transactionService,
        ApplicationDbContext context,
        IWebHostEnvironment env)
    {
        _transactionService = transactionService;
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

        var (transactions, totalCount) = await _transactionService.GetTransactionsAsync(
            userId, month, year, type, categoryId, keyword, page, pageSize);

        var (totalIncome, totalExpense, balance) = await _transactionService.GetBalanceAsync(userId);

        // Danh sách danh mục khả dụng của user
        var categories = await _context.Categories
            .Where(c => (c.IsDefault || c.UserId == userId) && c.IsActive)
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Name)
            .ToListAsync();

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

        // Nếu là yêu cầu AJAX (chuyển trang hoặc lọc), trả về PartialView
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return PartialView("_TransactionListPartial", transactions);
        }

        return View(transactions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Transaction transaction, IFormFile? invoiceFile)
    {
        int userId = GetCurrentUserId();
        transaction.UserId = userId; // Đảm bảo luôn lấy UserId từ phiên đăng nhập

        var result = await _transactionService.CreateTransactionAsync(transaction, invoiceFile, _env.WebRootPath);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Transaction transaction, IFormFile? invoiceFile)
    {
        int userId = GetCurrentUserId();

        var result = await _transactionService.UpdateTransactionAsync(transaction, invoiceFile, _env.WebRootPath, userId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        int userId = GetCurrentUserId();

        var result = await _transactionService.DeleteTransactionAsync(id, userId);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> GetById(int id)
    {
        int userId = GetCurrentUserId();
        var trx = await _transactionService.GetTransactionByIdAsync(id, userId);
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
