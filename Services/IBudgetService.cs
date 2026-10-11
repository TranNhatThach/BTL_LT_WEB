using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Services;

public interface IBudgetService
{
    Task CheckAndTriggerBudgetAlertAsync(int userId, int categoryId, DateTime transactionDate);
    Task EnsureBudgetsInheritedAsync(int userId, int targetMonth, int targetYear);
    Task<List<BudgetProgressViewModel>> GetBudgetsWithProgressAsync(int userId, int month, int year);
    Task<(bool Success, string Message)> CreateBudgetAsync(Budget budget, int userId);
    Task<(bool Success, string Message)> UpdateBudgetAsync(Budget budget, int userId);
    Task<(bool Success, string Message)> DeleteBudgetAsync(int id, int userId);
}

public class BudgetProgressViewModel
{
    public Budget Budget { get; set; } = null!;
    public decimal SpentAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public double Percentage { get; set; }
    public string StatusLevel { get; set; } = "Normal"; // "Normal", "Warning", "Exceeded"
}
