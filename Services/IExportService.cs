using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Services;

public interface IExportService
{
    byte[] ExportMonthlyReportToExcel(AppUser user, int month, int year, List<Transaction> transactions);
}
