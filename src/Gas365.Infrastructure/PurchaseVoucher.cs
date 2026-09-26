using System.Globalization;
using Gas365.Application;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Gas365.Infrastructure;

public static class PurchaseVoucher
{
    static PurchaseVoucher() => GlobalFontSettings.FontResolver = new VoucherFontResolver();
    internal static void EnsureFonts() { }
    public static byte[] Render(PurchaseDto purchase)
    {
        using var pdf = new PdfDocument();
        pdf.Info.Title = "Gas365 Purchase Voucher";
        var font = new XFont("Gas365", 10); var heading = new XFont("Gas365", 18);
        var navy = new XSolidBrush(XColor.FromArgb(18, 56, 90));
        XGraphics? graphics = null; double y = 0;
        void NewPage()
        {
            graphics?.Dispose(); var page = pdf.AddPage(); page.Size = PdfSharp.PageSize.A4;
            graphics = XGraphics.FromPdfPage(page); y = 82;
            graphics.DrawString("Gas365 | Purchase voucher", heading, navy, 40, 42);
            graphics.DrawString($"Page {pdf.PageCount}  |  {purchase.Status}", font, navy, 40, 62);
        }
        void Text(string text)
        {
            // Wrap by measured glyph width, including unbroken references.
            var line = "";
            foreach (var ch in text.Replace('\r', ' ').Replace('\n', ' '))
            {
                if (graphics!.MeasureString(line + ch, font).Width > 510)
                { if (y > 790) NewPage(); graphics!.DrawString(line, font, XBrushes.Black, 40, y); y += 16; line = ""; }
                line += ch;
            }
            if (y > 790) NewPage(); graphics!.DrawString(line, font, XBrushes.Black, 40, y); y += 18;
        }
        string Money(decimal amount) => "PKR " + amount.ToString("N2", CultureInfo.InvariantCulture);
        NewPage();
        Text("Purchase: " + purchase.Id); Text("Supplier: " + purchase.SupplierName);
        Text("Reference: " + purchase.Snapshot.Reference);
        Text("Business date: " + purchase.Entries.First(x => x.Kind == "Invoice").BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        if (purchase.CorrectsId.HasValue) Text("Corrects purchase: " + purchase.CorrectsId);
        Text("Allocation: " + purchase.Snapshot.AllocationRule + " | Landed charges: " + Money(purchase.Snapshot.LandedCharges));
        Text("Notes: " + purchase.Snapshot.Notes); y += 8;
        foreach (var (line, i) in purchase.Snapshot.Lines.Select((line, i) => (line, i)))
        {
            if (y > 690) NewPage();
            Text($"{i + 1}. {line.Sku} | {line.Name}");
            Text($"Location: {line.Warehouse} | State: {line.Bucket}");
            Text($"Quantity: {line.Quantity.ToString(CultureInfo.InvariantCulture)} {line.Unit} | Unit price: {line.Price.ToString("0.######", CultureInfo.InvariantCulture)} PKR");
            Text($"Base: {Money(line.BaseValue)} | Allocated: {Money(line.LandedCost)} | Value: {Money(line.Value)}");
            if (line.Serials.Length > 0) Text("Serials: " + string.Join(", ", line.Serials));
            y += 8;
        }
        Text("Total: " + Money(purchase.Total)); Text("Net cash paid: " + Money(purchase.Paid)); Text("Outstanding: " + Money(purchase.Outstanding));
        if (purchase.SeparateCosts)
        {
            Text("Purchase costs are paid separately from the goods supplier and included in stock value.");
            Text("Separate costs paid: " + Money(purchase.CostsPaid) + " | Unpaid: " + Money(purchase.CostsOutstanding));
        }
        Text("Payment and correction history");
        foreach (var entry in purchase.Entries)
            Text($"{entry.BusinessDate:yyyy-MM-dd} | {entry.Kind} | Payable {Money(entry.Payable)} | Cash {Money(entry.Cash)} | {entry.Reason}");
        Text("Generated from recorded purchase and payment entries. This is not a tax invoice.");
        graphics?.Dispose(); using var stream = new MemoryStream(); pdf.Save(stream, false); return stream.ToArray();
    }
    private sealed class VoucherFontResolver : IFontResolver
    {
        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("Vera");
        public byte[] GetFont(string faceName)
        {
            using var stream = typeof(PurchaseVoucher).Assembly.GetManifestResourceStream("Gas365.Infrastructure.Fonts.Vera.ttf")!;
            using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
        }
    }
}
