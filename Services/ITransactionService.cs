using BTL_LTWEB_QLChiTieu.Models;
using Microsoft.AspNetCore.Http;

namespace BTL_LTWEB_QLChiTieu.Services;

public interface ITransactionService
{
    Task<(decimal TotalIncome, decimal TotalExpense, decimal Balance)> GetBalanceAsync(int userId);
    
    Task<(List<Transaction> Transactions, int TotalCount)> GetTransactionsAsync(
        int userId, int? month, int? year, string? type, int? categoryId, string? keyword, int page, int pageSize);
        
    Task<Transaction?> GetTransactionByIdAsync(int id, int userId);
    
    Task<(bool Success, string Message, Transaction? Transaction)> CreateTransactionAsync(
        Transaction transaction, IFormFile? invoiceFile, string uploadsFolder);
        
    Task<(bool Success, string Message)> UpdateTransactionAsync(
        Transaction transaction, IFormFile? invoiceFile, string uploadsFolder, int userId);
        
    Task<(bool Success, string Message)> DeleteTransactionAsync(int id, int userId);
}
