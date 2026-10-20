namespace Sugarista.Application.Common.Interfaces;

/// <summary>
/// One worksheet of an exported workbook: headers plus plain typed rows
/// (strings, numbers and booleans only — the renderer styles everything).
/// </summary>
public record ExcelSheet(
    string Name,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<object?>> Rows);

/// <summary>Rendered workbook file.</summary>
public record ExcelWorkbookFile(byte[] Content, string FileName);

/// <summary>
/// Excel renderer abstraction (implemented with ClosedXML in Infrastructure).
/// </summary>
public interface IExcelExporter
{
    ExcelWorkbookFile BuildWorkbook(string title, IReadOnlyCollection<ExcelSheet> sheets);
}
