using ClosedXML.Excel;
using BTL_LTWEB_QLChiTieu.Models;

namespace BTL_LTWEB_QLChiTieu.Services;

public class ExportService : IExportService
{
    public byte[] ExportMonthlyReportToExcel(AppUser user, int month, int year, List<Transaction> transactions)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add($"BaoCao_T{month}_{year}");

        // 1. Tiêu đề chính
        ws.Cell(1, 1).Value = "BÁO CÁO THU CHI TÀI CHÍNH CÁ NHÂN";
        ws.Range(1, 1, 1, 6).Merge().Style
            .Font.SetBold(true)
            .Font.SetFontSize(16)
            .Font.SetFontColor(XLColor.FromHtml("#0D6EFD"))
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // 2. Thông tin sinh viên & Kỳ báo cáo
        ws.Cell(2, 1).Value = $"Kỳ báo cáo: Tháng {month}/{year}";
        ws.Range(2, 1, 2, 6).Merge().Style
            .Font.SetItalic(true)
            .Font.SetFontSize(11)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        ws.Cell(4, 1).Value = "Họ và tên:";
        ws.Cell(4, 2).Value = user.FullName;
        ws.Cell(4, 4).Value = "Mã sinh viên:";
        ws.Cell(4, 5).Value = user.StudentCode ?? "-";

        ws.Cell(5, 1).Value = "Trường học:";
        ws.Cell(5, 2).Value = user.SchoolName ?? "-";
        ws.Cell(5, 4).Value = "Lớp:";
        ws.Cell(5, 5).Value = user.ClassName ?? "-";

        ws.Range(4, 1, 5, 1).Style.Font.SetBold(true);
        ws.Range(4, 4, 5, 4).Style.Font.SetBold(true);

        // 3. Tiêu đề các cột dữ liệu
        int headerRow = 7;
        string[] headers = { "STT", "Ngày giao dịch", "Loại", "Danh mục", "Ghi chú", "Số tiền (VNĐ)" };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold(true)
                .Font.SetFontColor(XLColor.White)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#0D6EFD"))
                .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        }

        // 4. Đổ dữ liệu giao dịch
        int currentRow = headerRow + 1;
        int stt = 1;
        decimal totalIncome = 0;
        decimal totalExpense = 0;

        foreach (var t in transactions)
        {
            ws.Cell(currentRow, 1).Value = stt++;
            ws.Cell(currentRow, 1).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(currentRow, 2).Value = t.TransactionDate.ToString("dd/MM/yyyy");
            ws.Cell(currentRow, 2).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(currentRow, 3).Value = t.Type == "Income" ? "Thu nhập" : "Chi tiêu";
            ws.Cell(currentRow, 3).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

            ws.Cell(currentRow, 4).Value = t.Category?.Name ?? "Khác";
            ws.Cell(currentRow, 5).Value = t.Note ?? "";

            var amountCell = ws.Cell(currentRow, 6);
            amountCell.Value = t.Amount;
            amountCell.Style.NumberFormat.Format = "#,##0";
            amountCell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);

            if (t.Type == "Income")
            {
                totalIncome += t.Amount;
                ws.Cell(currentRow, 3).Style.Font.SetFontColor(XLColor.FromHtml("#198754"));
            }
            else
            {
                totalExpense += t.Amount;
                ws.Cell(currentRow, 3).Style.Font.SetFontColor(XLColor.FromHtml("#DC3545"));
            }

            currentRow++;
        }

        // Kẻ viền bảng dữ liệu
        if (transactions.Any())
        {
            ws.Range(headerRow, 1, currentRow - 1, 6).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);
            ws.Range(headerRow, 1, currentRow - 1, 6).Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
        }

        // 5. Tổng kết cuối báo cáo
        currentRow += 1;
        ws.Cell(currentRow, 4).Value = "TỔNG THU NHẬP:";
        ws.Cell(currentRow, 4).Style.Font.SetBold(true);
        ws.Cell(currentRow, 6).Value = totalIncome;
        ws.Cell(currentRow, 6).Style.Font.SetBold(true).Font.SetFontColor(XLColor.FromHtml("#198754")).NumberFormat.SetFormat("#,##0");

        currentRow++;
        ws.Cell(currentRow, 4).Value = "TỔNG CHI TIÊU:";
        ws.Cell(currentRow, 4).Style.Font.SetBold(true);
        ws.Cell(currentRow, 6).Value = totalExpense;
        ws.Cell(currentRow, 6).Style.Font.SetBold(true).Font.SetFontColor(XLColor.FromHtml("#DC3545")).NumberFormat.SetFormat("#,##0");

        currentRow++;
        ws.Cell(currentRow, 4).Value = "CHÊNH LỆCH (DƯ/HỤT):";
        ws.Cell(currentRow, 4).Style.Font.SetBold(true);
        var diffCell = ws.Cell(currentRow, 6);
        diffCell.Value = totalIncome - totalExpense;
        diffCell.Style.Font.SetBold(true).NumberFormat.SetFormat("#,##0");
        if (totalIncome - totalExpense >= 0)
        {
            diffCell.Style.Font.SetFontColor(XLColor.FromHtml("#0D6EFD"));
        }
        else
        {
            diffCell.Style.Font.SetFontColor(XLColor.FromHtml("#DC3545"));
        }

        // Tự động căn chỉnh độ rộng các cột
        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
