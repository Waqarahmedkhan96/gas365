using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Gas365.Application;
using Gas365.Domain;
using Gas365.Infrastructure;
using PdfSharp.Pdf.IO;

namespace Gas365.UnitTests;

public sealed class ReportTests
{
    private static ReportData Data(int rows, string text = "Domestic LPG")
    {
        var definition = ReportAccess.Catalog.Single(x => x.Key == "daily-sales");
        ReportColumn[] columns = [new("item", "Item", "text"), new("quantity", "Quantity", "quantity"), new("value", "Value", "money"), new("margin", "Margin %", "percent")];
        var list = Enumerable.Range(1, rows).Select(i => new object?[] { $"{text} {i}", 1.5m * i, 1234.5m * i, 12.34m }).ToList();
        return new(definition, new(null, new(2026, 9, 1), new(2026, 9, 30), null), "All accessible branches", columns, list, [null, null, list.Sum(r => (decimal)r[2]!), null]);
    }

    [Theory]
    [InlineData("SaleCash", "Sales and collections")]
    [InlineData("CustomerReceiptReversal", "Customer advances")]
    [InlineData("CylinderCash", "Cylinder deposits, fees and refunds")]
    [InlineData("PurchaseCash", "Purchases and purchase costs")]
    [InlineData("LoanRepayment", "Loans and owner funds")]
    [InlineData("OwnerWithdrawalReversal", "Loans and owner funds")]
    [InlineData("TransferInReversal", "Transfers and bank deposits")]
    [InlineData("CashAdjustmentOut", "Adjustments")]
    [InlineData("Opening", "Opening balance")]
    public void CashKindsMapToTheReconciliationFormulaLines(string kind, string expected) =>
        Assert.Equal(expected, ReportCalculation.CashCategory(kind));

    [Fact]
    public void CategoriesAndMarginsFollowPublishedRules()
    {
        Assert.Equal(ReportCalculation.SpareParts, ReportCalculation.SaleCategory("SparePart"));
        Assert.All(new[] { "Refill", "FullCylinder", "CylinderOnly", "Exchange", "LoanRefill" }, m => Assert.Equal(ReportCalculation.Lpg, ReportCalculation.SaleCategory(m)));
        Assert.Null(ReportCalculation.Margin(0, 5));
        Assert.Equal(33.33m, ReportCalculation.Margin(300, 100));
        Assert.Equal(-12.5m, ReportCalculation.Margin(80, -10));
    }

    [Fact]
    public void EveryReportHasADefinitionReconciliationAndPermission()
    {
        Assert.Equal(15, ReportAccess.Catalog.Length);
        Assert.Equal(ReportAccess.Catalog.Length, ReportAccess.Catalog.Select(x => x.Key).Distinct().Count());
        Assert.All(ReportAccess.Catalog, d => { Assert.NotEmpty(d.Definition); Assert.NotEmpty(d.Reconciliation); Assert.NotEmpty(d.Permissions); });
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+92 300", "'+92 300")]
    [InlineData("-cmd", "'-cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("Ali, Sons", "\"Ali, Sons\"")]
    [InlineData("Line\nbreak", "\"Line\nbreak\"")]
    [InlineData("Plain", "Plain")]
    public void CsvCellsAreEscapedAndCannotBecomeFormulas(string value, string expected) => Assert.Equal(expected, ReportFiles.CsvCell(value));

    [Fact]
    public void CsvKeepsNegativeNumbersNumericAndAddsTotals()
    {
        Assert.Equal("-10.50", ReportFiles.CsvCell(-10.50m));
        var text = Encoding.UTF8.GetString(ReportFiles.Render(Data(2, "علی گیس"), "csv", DateTimeOffset.UtcNow));
        Assert.StartsWith("﻿Item,Quantity,Value,Margin %", text);
        Assert.Contains("علی گیس 1,1.5,1234.5,12.34", text);
        Assert.Contains("Total,,3703.5,", text);
    }

    [Fact]
    public void XlsxStoresNumbersAsNumbersWithAnAboutSheet()
    {
        var bytes = ReportFiles.Render(Data(3), "xlsx", DateTimeOffset.UtcNow);
        using var document = SpreadsheetDocument.Open(new MemoryStream(bytes), false);
        var workbook = document.WorkbookPart!;
        var sheets = workbook.Workbook!.Sheets!.Elements<Sheet>().Select(s => s.Name!.Value!).ToArray();
        Assert.Equal(new[] { "Report", "About" }, sheets);
        var rows = workbook.WorksheetParts.First().Worksheet!.Descendants<Row>().ToArray();
        Assert.Equal(5, rows.Length); // Header, three rows and totals.
        var value = rows[1].Elements<Cell>().ElementAt(2);
        Assert.Equal(CellValues.Number, value.DataType!.Value); Assert.Equal("1234.5", value.CellValue!.Text);
        Assert.Equal("7407.0", rows[4].Elements<Cell>().ElementAt(2).CellValue!.Text);
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(400, 8)]
    public void PdfPaginatesLandscapeTables(int rows, int minimumPages)
    {
        var bytes = ReportFiles.Render(Data(rows, "Commercial LPG with a deliberately long item name " + new string('W', 80)), "pdf", DateTimeOffset.UtcNow);
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.Equal("Gas365 Daily sales", pdf.Info.Title);
        Assert.True(pdf.PageCount >= minimumPages);
        Assert.True(pdf.Pages[0].Width.Point > pdf.Pages[0].Height.Point);
    }

    [Fact]
    public void FileNamesCarryReportAndPeriod() =>
        Assert.Equal("gas365-profit-20260901-20260930.xlsx", ReportFiles.FileName("profit", new(null, new(2026, 9, 1), new(2026, 9, 30), null), "xlsx"));
}
