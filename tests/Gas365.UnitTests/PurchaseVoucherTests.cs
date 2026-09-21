using Gas365.Application;
using Gas365.Infrastructure;
using PdfSharp.Pdf.IO;

namespace Gas365.UnitTests;

public sealed class PurchaseVoucherTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(100)]
    public void VoucherSupportsMultiplePagesAndLongText(int count)
    {
        var lines = Enumerable.Range(1, count).Select(i => new PurchaseLineDto(Guid.NewGuid(), $"SKU-{i}", "Domestic LPG - " + new string('W', 90), "KG", Guid.NewGuid(), "Main warehouse", "Available", 10, 100, 10, [], 1000, 5, 1005, 100.5m)).ToArray();
        var date = new DateOnly(2026, 9, 20);
        var entry = new PurchaseEntryDto(Guid.NewGuid(), "Invoice", 1005 * count, 0, "Supplier invoice", Guid.NewGuid(), DateTimeOffset.UtcNow, date, null);
        var purchase = new PurchaseDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Synthetic supplier", "Posted", 0, 1005 * count, 0, 1005 * count,
            new("Reference " + new string('R', 110), "Long reference and item names must wrap without clipping.", "Quantity", 5 * count, lines), Guid.NewGuid(), null, [entry]);
        var bytes = PurchaseVoucher.Render(purchase);
        using var pdf = PdfReader.Open(new MemoryStream(bytes), PdfDocumentOpenMode.Import);
        Assert.Equal("Gas365 Purchase Voucher", pdf.Info.Title);
        if (count == 100) Assert.True(pdf.PageCount > 10); else Assert.Equal(1, pdf.PageCount);
        var output = Environment.GetEnvironmentVariable("GAS365_VOUCHER_QA_DIR");
        if (output is not null) { Directory.CreateDirectory(output); File.WriteAllBytes(Path.Combine(output, $"voucher-{count}-lines.pdf"), bytes); }
    }
}
