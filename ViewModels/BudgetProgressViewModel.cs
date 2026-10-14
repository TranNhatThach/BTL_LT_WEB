using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.ViewModels;

/// <summary>
/// ViewModel chứa dữ liệu tiến độ thực chi so với hạn mức ngân sách của một danh mục trong tháng
/// </summary>
public class BudgetProgressViewModel
{
    // Thông tin ngân sách đã thiết lập (Danh mục, Tháng, Năm, Hạn mức)
    public Budget Budget { get; set; } = null!;

    // Tổng số tiền đã chi thực tế trong tháng
    public decimal SpentAmount { get; set; }

    // Số tiền còn lại có thể chi (Hạn mức - Đã chi)
    public decimal RemainingAmount { get; set; }

    // Tỷ lệ phần trăm đã sử dụng (Đã chi / Hạn mức * 100)
    public double Percentage { get; set; }

    // Cấp độ cảnh báo: "Normal" (< 80%), "Warning" (80% - 99.9%), "Exceeded" (>= 100%)
    public string StatusLevel { get; set; } = "Normal";
}
