using Gas365.Application;
using Gas365.Infrastructure;
using PdfSharp.Pdf.IO;

namespace Gas365.UnitTests;

public sealed class SaleReceiptTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void ReceiptWrapsLongNamesAndPaginates(int count)
    {
        var lines = Enumerable.Range(1, count).Select(i => new SaleLineDto(Guid.NewGuid(), 1, Guid.NewGuid(), $"LPG-{i}", "Cylinder filling " + new string('W', 90), "Refill", "KG", Guid.NewGuid(), "Main", 1.6m, 200, 320, 0, 320, "Customer cylinder " + i, 0.1m, "Filling loss")).ToArray();
        var entry = new SaleEntryDto(Guid.NewGuid(), "Invoice", count * 320, 0, count * 320, null, "Sale", Guid.NewGuid(), DateTimeOffset.UtcNow, new(2026, 9, 21), null);
        var sale = new SaleDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('R', 120), "Posted", count * 320, 0, count * 320, null, null, Guid.NewGuid(), new("Synthetic customer", "Retail", "Wrap test", lines, count * 320, 0, count * 320), [entry]);
        var bytes = SaleReceipt.Render(sale);
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.Equal("Gas365 Sales Receipt", pdf.Info.Title);
        if (count == 100) Assert.True(pdf.PageCount > 5); else Assert.Equal(1, pdf.PageCount);
        var output = Environment.GetEnvironmentVariable("GAS365_VOUCHER_QA_DIR");
        if (output is not null) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"sale-{count}-lines.pdf"), bytes); }
    }
}
