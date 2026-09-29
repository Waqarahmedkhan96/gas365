using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Gas365.Application;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Gas365.Infrastructure;

internal static class ReportFiles
{
    internal const int MaxPdfRows = 5_000;
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    internal static string FileName(string key, ReportFilter f, string format) =>
        $"gas365-{key}-{f.From:yyyyMMdd}-{f.To:yyyyMMdd}.{format}";

    internal static string ContentType(string format) => format switch
    {
        "csv" => "text/csv; charset=utf-8",
        "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/pdf"
    };

    internal static byte[] Render(ReportData data, string format, DateTimeOffset generatedAt) => format switch
    {
        "csv" => Csv(data),
        "xlsx" => Xlsx(data, generatedAt),
        "pdf" => Pdf(data, generatedAt),
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };

    private static IEnumerable<object?[]> WithTotals(ReportData data) =>
        data.Totals.Any(x => x is not null) ? data.Rows.Append(data.Totals.Select((t, i) => i == 0 && t is null ? "Total" : t).ToArray()) : data.Rows;

    internal static string Format(object? value, string type) => value switch
    {
        null => "",
        decimal d when type == "money" => d.ToString("N2", Invariant),
        decimal d when type == "percent" => d.ToString("0.00", Invariant) + "%",
        decimal d => d.ToString("0.######", Invariant),
        IFormattable x => x.ToString(null, Invariant),
        _ => value.ToString() ?? ""
    };

    // Text that a spreadsheet would evaluate as a formula is prefixed with an apostrophe (OWASP CSV injection guidance).
    internal static string CsvCell(object? value)
    {
        var text = value switch { null => "", decimal d => d.ToString(Invariant), int i => i.ToString(Invariant), _ => value.ToString() ?? "" };
        if (value is string && text.Length > 0 && "=+-@\t\r".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }

    private static byte[] Csv(ReportData data)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', data.Columns.Select(c => CsvCell(c.Label))));
        foreach (var row in WithTotals(data)) builder.AppendLine(string.Join(',', row.Select(CsvCell)));
        // UTF-8 with BOM so spreadsheet tools detect the encoding of Urdu names.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    private static byte[] Xlsx(ReportData data, DateTimeOffset generatedAt)
    {
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart(); workbook.Workbook = new Workbook();
            var styles = workbook.AddNewPart<WorkbookStylesPart>(); styles.Stylesheet = Styles(); styles.Stylesheet.Save();
            var sheets = workbook.Workbook.AppendChild(new Sheets());
            void Sheet(uint id, string name, IEnumerable<Row> rows)
            {
                var part = workbook.AddNewPart<WorksheetPart>(); var sheetData = new SheetData(rows);
                part.Worksheet = new Worksheet(sheetData); part.Worksheet.Save();
                sheets.Append(new Sheet { Id = workbook.GetIdOfPart(part), SheetId = id, Name = name });
            }
            var header = new Row(data.Columns.Select(c => TextCell(c.Label, 1)));
            var rows = WithTotals(data).Select((row, index) => new Row(row.Select((value, i) => ValueCell(value, data.Columns[i].Type, index == data.Rows.Count))));
            Sheet(1, "Report", [header, .. rows]);
            Sheet(2, "About", new[]
            {
                ("Report", data.Definition.Title), ("Definition", data.Definition.Definition), ("Reconciliation", data.Definition.Reconciliation),
                ("Scope", data.Scope), ("From", data.Filter.From.ToString("yyyy-MM-dd", Invariant)), ("To", data.Filter.To.ToString("yyyy-MM-dd", Invariant)),
                ("Type filter", data.Filter.Type ?? "All"), ("Generated (UTC)", generatedAt.ToString("yyyy-MM-dd HH:mm", Invariant))
            }.Select(x => new Row(TextCell(x.Item1, 1), TextCell(x.Item2, 0))));
            workbook.Workbook.Save();
        }
        return stream.ToArray();
    }

    private static Cell TextCell(string text, uint style) => new() { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text)), StyleIndex = style };

    // Numbers stay numeric so totals can be recalculated; styles: 2 money, 3 quantity, 4 percent, 5 cost; +4 for the bold total row.
    private static Cell ValueCell(object? value, string type, bool total) => value switch
    {
        decimal d => new Cell { CellValue = new CellValue(d.ToString(Invariant)), DataType = CellValues.Number, StyleIndex = (uint)((type switch { "money" => 2, "percent" => 4, "cost" => 5, _ => 3 }) + (total ? 4 : 0)) },
        int i => new Cell { CellValue = new CellValue(i.ToString(Invariant)), DataType = CellValues.Number, StyleIndex = total ? 1u : 0u },
        _ => TextCell(value?.ToString() ?? "", total ? 1u : 0u)
    };

    private static Stylesheet Styles()
    {
        var formats = new NumberingFormats(
            new NumberingFormat { NumberFormatId = 164, FormatCode = "#,##0.00" },
            new NumberingFormat { NumberFormatId = 165, FormatCode = "#,##0.000" },
            new NumberingFormat { NumberFormatId = 166, FormatCode = "0.00" },
            new NumberingFormat { NumberFormatId = 167, FormatCode = "#,##0.000000" });
        var fonts = new Fonts(new Font(), new Font(new Bold()));
        CellFormat Format(uint number, uint font) => new() { NumberFormatId = number, FontId = font, ApplyNumberFormat = number != 0, ApplyFont = font != 0 };
        return new Stylesheet(formats, fonts, new Fills(new Fill(new PatternFill { PatternType = PatternValues.None }), new Fill(new PatternFill { PatternType = PatternValues.Gray125 })),
            new Borders(new Border()),
            new CellFormats(Format(0, 0), Format(0, 1), Format(164, 0), Format(165, 0), Format(166, 0), Format(167, 0), Format(164, 1), Format(165, 1), Format(166, 1), Format(167, 1)));
    }

    private static byte[] Pdf(ReportData data, DateTimeOffset generatedAt)
    {
        PurchaseVoucher.EnsureFonts();
        using var pdf = new PdfDocument(); pdf.Info.Title = "Gas365 " + data.Definition.Title;
        var font = new XFont("Gas365", 8); var bold = new XFont("Gas365", 8, XFontStyleEx.Bold); var heading = new XFont("Gas365", 15);
        var navy = new XSolidBrush(XColor.FromArgb(18, 56, 90)); var rule = new XPen(XColor.FromArgb(228, 234, 240), 0.5);
        const double left = 30, width = 782, bottom = 560;
        // Text columns get twice the width of numeric columns.
        var weights = data.Columns.Select(c => c.Type == "text" ? 2.0 : 1.0).ToArray(); var unit = width / weights.Sum();
        XGraphics? g = null; double y = 0;
        string Fit(string text, double max, XFont f)
        {
            if (g!.MeasureString(text, f).Width <= max) return text;
            while (text.Length > 1 && g.MeasureString(text + "…", f).Width > max) text = text[..^1];
            return text + "…";
        }
        void Row(object?[] cells, XFont f)
        {
            double x = left;
            for (var i = 0; i < cells.Length; i++)
            {
                var w = weights[i] * unit; var text = Fit(Format(cells[i], data.Columns[i].Type), w - 4, f);
                var numeric = data.Columns[i].Type != "text" && data.Columns[i].Type != "date";
                g!.DrawString(text, f, XBrushes.Black, new XRect(x + 2, y - 9, w - 4, 12), numeric ? XStringFormats.TopRight : XStringFormats.TopLeft);
                x += w;
            }
            g!.DrawLine(rule, left, y + 4, left + width, y + 4); y += 13;
        }
        void Page(bool first)
        {
            g?.Dispose(); var page = pdf.AddPage(); page.Size = PdfSharp.PageSize.A4; page.Orientation = PdfSharp.PageOrientation.Landscape;
            g = XGraphics.FromPdfPage(page); y = 40;
            g.DrawString("Gas365 | " + data.Definition.Title, heading, navy, left, y); y += 16;
            g.DrawString($"{data.Scope} · {data.Filter.From:yyyy-MM-dd} to {data.Filter.To:yyyy-MM-dd} · Type: {data.Filter.Type ?? "All"} · Generated {generatedAt:yyyy-MM-dd HH:mm} UTC · Page {pdf.PageCount}", font, navy, left, y); y += 16;
            if (first) { foreach (var line in Wrap(data.Definition.Definition, 170)) { g.DrawString(line, font, XBrushes.DimGray, left, y); y += 11; } y += 8; }
            else y += 4;
            Row(data.Columns.Select(c => (object?)c.Label).ToArray(), bold);
        }
        Page(true);
        foreach (var (row, index) in WithTotals(data).Select((r, i) => (r, i)))
        {
            if (y > bottom) Page(false);
            Row(row, index == data.Rows.Count ? bold : font);
        }
        if (data.Rows.Count == 0) g!.DrawString("No activity for these filters.", font, XBrushes.Black, left, y);
        g?.Dispose();
        using var stream = new MemoryStream(); pdf.Save(stream); return stream.ToArray();
    }

    private static IEnumerable<string> Wrap(string text, int length)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' '))
        {
            if (line.Length + word.Length > length) { yield return line.ToString().TrimEnd(); line.Clear(); }
            line.Append(word).Append(' ');
        }
        if (line.Length > 0) yield return line.ToString().TrimEnd();
    }
}
