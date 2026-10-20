using ClosedXML.Excel;
using Sugarista.Application.Common.Interfaces;

namespace Sugarista.Infrastructure.Services;

/// <summary>
/// Renders result sets into a styled, right-to-left Excel workbook (ClosedXML).
/// Used by the admin export center for legal/backup reporting.
/// </summary>
public class ClosedXmlExcelExporter : IExcelExporter
{
    public ExcelWorkbookFile BuildWorkbook(string title, IReadOnlyCollection<ExcelSheet> sheets)
    {
        using var workbook = new XLWorkbook
        {
            RightToLeft = true, // applies to all worksheets (Persian reports)
        };
        var metaSheet = workbook.Worksheets.Add("شناسنامه");
        metaSheet.Cell(1, 1).Value = "عنوان خروجی";
        metaSheet.Cell(1, 2).Value = title;
        metaSheet.Cell(2, 1).Value = "سیستم";
        metaSheet.Cell(2, 2).Value = "Sugarista — پنل مدیریت";
        metaSheet.Cell(3, 1).Value = "ساعت خروجی";
        metaSheet.Cell(3, 2).Value = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        metaSheet.Cell(4, 1).Value = "برگه‌ها";
        metaSheet.Cell(4, 2).Value = string.Join("، ", sheets.Select(sheet => sheet.Name));
        metaSheet.Cell(5, 1).Value = "توصیه";
        metaSheet.Cell(5, 2).Value = "این فایل به‌صورت خودکار تولید شده و برای ارائه به مراجع قانونی و بک‌آپ‌گیری مناسب است.";
        metaSheet.Columns().AdjustToContents();

        foreach (var sheetData in sheets)
        {
            var worksheet = workbook.Worksheets.Add(SanitizeSheetName(sheetData.Name));

            // title row + header row
            worksheet.Cell(1, 1).Value = title;
            worksheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
            worksheet.Range(1, 1, 1, Math.Max(1, sheetData.Headers.Count)).Merge();
            worksheet.Range(2, 1, 2, sheetData.Headers.Count).Style
                .Font.SetBold()
                .Fill.SetBackgroundColor(XLColor.FromHtml("#2F6F8F"))
                .Font.SetFontColor(XLColor.White);

            for (var header = 0; header < sheetData.Headers.Count; header++)
            {
                worksheet.Cell(2, header + 1).Value = sheetData.Headers[header];
            }

            for (var row = 0; row < sheetData.Rows.Count; row++)
            {
                var values = sheetData.Rows[row];
                for (var column = 0; column < values.Count; column++)
                {
                    var cell = worksheet.Cell(row + 3, column + 1);
                    var value = values[column];
                    if (value is null)
                    {
                        continue;
                    }

                    cell.Value = value switch
                    {
                        string text => text,
                        bool flag => flag,
                        int intV => intV,
                        long longV => (double)longV,
                        decimal decV => decV,
                        double dbV => dbV,
                        _ => value.ToString() ?? string.Empty,
                    };
                }

                if (row % 2 == 1)
                {
                    worksheet.Range(row + 3, 1, row + 3, sheetData.Headers.Count).Style
                        .Fill.SetBackgroundColor(XLColor.FromHtml("#F0F6FA"));
                }
            }

            if (sheetData.Rows.Count > 0)
            {
                var lastRow = sheetData.Rows.Count + 2;
                var lastCol = Math.Max(1, sheetData.Headers.Count);
                worksheet.Range(2, 1, lastRow, lastCol).SetAutoFilter();
            }

            worksheet.SheetView.FreezeRows(2);
            worksheet.Columns().AdjustToContents();
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"sugarista-export-{DateTimeOffset.Now:yyyyMMdd-HHmm}.xlsx";
        return new ExcelWorkbookFile(stream.ToArray(), fileName);
    }

    private static string SanitizeSheetName(string name)
    {
        var safe = name.Replace(":", " ").Replace("\\", " ").Replace("/", " ")
            .Replace("?", " ").Replace("*", " ").Replace("[", " ").Replace("]", " ").Trim();
        return safe.Length <= 31 ? safe : safe[..31];
    }
}
