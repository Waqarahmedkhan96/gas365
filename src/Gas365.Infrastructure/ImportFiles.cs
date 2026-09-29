using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Gas365.Application;
using Gas365.Domain;

namespace Gas365.Infrastructure;

internal sealed record ParsedFile(string[] Headers, List<string[]> Rows);

// Reads uploaded workbooks without trusting the file name: content must match the declared type.
internal static class ImportFiles
{
    private const int MaxColumns = 50;
    private const int MaxCell = 1_000;

    internal static ParsedFile Parse(byte[] bytes, string fileName)
    {
        var name = fileName.ToLowerInvariant();
        List<string[]> table;
        if (name.EndsWith(".xlsx", StringComparison.Ordinal))
        {
            if (bytes.Length < 4 || bytes[0] != 'P' || bytes[1] != 'K' || bytes[2] != 3 || bytes[3] != 4) throw new AccessException(400, "The file is not a valid Excel workbook.");
            table = Xlsx(bytes);
        }
        else if (name.EndsWith(".csv", StringComparison.Ordinal))
        {
            string text;
            try { text = new UTF8Encoding(false, true).GetString(bytes); }
            catch (DecoderFallbackException) { throw new AccessException(400, "Save the CSV file as UTF-8."); }
            if (text.Contains('\0')) throw new AccessException(400, "The file is not a text CSV file.");
            table = Csv(text.TrimStart('﻿'));
        }
        else if (name.EndsWith(".xls", StringComparison.Ordinal)) throw new AccessException(400, "Save older .xls workbooks as .xlsx or .csv first.");
        else throw new AccessException(400, "Upload a .csv or .xlsx file.");

        table = table.Select(r => r.Select(c => c.Trim()).ToArray()).Where(r => r.Any(c => c.Length > 0)).ToList();
        if (table.Count == 0) throw new AccessException(400, "The file has no rows.");
        var headers = table[0];
        if (headers.Length > MaxColumns) throw new AccessException(400, $"Use at most {MaxColumns} columns.");
        if (headers.Any(h => h.Length == 0)) throw new AccessException(400, "Every column needs a heading in the first row.");
        if (headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length) throw new AccessException(400, "Column headings must be unique.");
        var rows = table.Skip(1).Select(r => headers.Select((_, i) => i < r.Length ? r[i] : "").ToArray()).ToList();
        if (rows.Count == 0) throw new AccessException(400, "The file has headings but no data rows.");
        if (rows.Count > ImportStatus.MaxRows) throw new AccessException(400, $"Import at most {ImportStatus.MaxRows:N0} rows per file.");
        if (rows.Any(r => r.Any(c => c.Length > MaxCell))) throw new AccessException(400, $"Cells may contain at most {MaxCell} characters.");
        return new(headers, rows);
    }

    // RFC 4180: quoted fields may contain commas, quotes ("") and line breaks.
    internal static List<string[]> Csv(string text)
    {
        var rows = new List<string[]>(); var row = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else cell.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); rows.Add([.. row]); row.Clear();
            }
            else cell.Append(c);
            if (rows.Count > ImportStatus.MaxRows + 1) throw new AccessException(400, $"Import at most {ImportStatus.MaxRows:N0} rows per file.");
        }
        if (quoted) throw new AccessException(400, "The CSV file has an unclosed quote.");
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add([.. row]); }
        return rows;
    }

    private static List<string[]> Xlsx(byte[] bytes)
    {
        try
        {
            using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
            var workbook = document.WorkbookPart ?? throw new AccessException(400, "The workbook has no sheets.");
            var sheet = workbook.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault() ?? throw new AccessException(400, "The workbook has no sheets.");
            var part = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
            var shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>().Select(x => x.InnerText).ToArray() ?? [];
            var rows = new List<string[]>();
            foreach (var row in part.Worksheet!.Descendants<Row>())
            {
                if (rows.Count > ImportStatus.MaxRows + 1) throw new AccessException(400, $"Import at most {ImportStatus.MaxRows:N0} rows per file.");
                var cells = new SortedDictionary<int, string>();
                var next = 0;
                foreach (var cell in row.Elements<Cell>())
                {
                    var index = cell.CellReference?.Value is { } reference ? Column(reference) : next;
                    if (index >= MaxColumns) throw new AccessException(400, $"Use at most {MaxColumns} columns.");
                    next = index + 1;
                    var raw = cell.CellValue?.Text ?? cell.InlineString?.InnerText ?? "";
                    cells[index] = cell.DataType?.Value == CellValues.SharedString && int.TryParse(raw, out var s) && s < shared.Length ? shared[s]
                        : cell.DataType?.Value == CellValues.Boolean ? (raw == "1" ? "TRUE" : "FALSE") : raw;
                }
                rows.Add(cells.Count == 0 ? [] : Enumerable.Range(0, cells.Keys.Max() + 1).Select(i => cells.GetValueOrDefault(i, "")).ToArray());
            }
            return rows;
        }
        catch (AccessException) { throw; }
        catch (Exception) { throw new AccessException(400, "The Excel workbook could not be read."); }
    }

    private static int Column(string reference)
    {
        var index = 0;
        foreach (var c in reference.TakeWhile(char.IsLetter)) index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        return index - 1;
    }

    // Accepts ISO dates, the Pakistani dd/mm/yyyy convention and Excel date serial numbers.
    internal static DateOnly? Date(string value)
    {
        if (DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) return date;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is > 20000 and < 80000)
            return DateOnly.FromDateTime(DateTime.FromOADate(Math.Floor(serial)));
        return null;
    }

    internal static decimal? Number(string value)
    {
        var text = value.Replace(",", "", StringComparison.Ordinal);
        if (!decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return null;
        // Excel stores binary doubles such as 0.10000000000000001; keep its 15 significant digits.
        if (text.Count(char.IsDigit) > 15 && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            number = decimal.Parse(d.ToString("G15", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture);
        return number / 1.000000000000000000000000000000000m; // Drops trailing zeros so precision checks see the real scale.
    }

    internal static bool? Flag(string value) => value.ToLowerInvariant() switch
    {
        "" or "no" or "false" or "0" or "n" => false,
        "yes" or "true" or "1" or "y" => true,
        _ => null
    };

    internal static string Normalize(string value) => new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
