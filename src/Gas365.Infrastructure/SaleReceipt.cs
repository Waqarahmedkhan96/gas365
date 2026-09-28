using System.Globalization;
using Gas365.Application;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Gas365.Infrastructure;

public static class SaleReceipt
{
    public static byte[] Render(SaleDto sale)
    {
        PurchaseVoucher.EnsureFonts();
        using var pdf = new PdfDocument(); pdf.Info.Title = "Gas365 Sales Receipt";
        var font = new XFont("Gas365", 10); var heading = new XFont("Gas365", 18);
        var navy = new XSolidBrush(XColor.FromArgb(18, 56, 90));
        XGraphics? graphics = null; double y = 0;
        void NewPage()
        {
            graphics?.Dispose(); var page = pdf.AddPage(); page.Size = PdfSharp.PageSize.A4;
            graphics = XGraphics.FromPdfPage(page); y = 82;
            graphics.DrawString("Gas365 | Sales receipt", heading, navy, 40, 42);
            graphics.DrawString($"Page {pdf.PageCount} | {sale.Status}", font, navy, 40, 62);
        }
        void Text(string value)
        {
            var line = "";
            foreach (var ch in value.Replace('\r', ' ').Replace('\n', ' '))
            {
                if (graphics!.MeasureString(line + ch, font).Width > 510)
                { if (y > 790) NewPage(); graphics!.DrawString(line, font, XBrushes.Black, 40, y); y += 16; line = ""; }
                line += ch;
            }
            if (y > 790) NewPage(); graphics!.DrawString(line, font, XBrushes.Black, 40, y); y += 18;
        }
        static string Money(decimal value) => "PKR " + value.ToString("N2", CultureInfo.InvariantCulture);
        NewPage(); Text("Receipt: " + sale.Id); Text("Reference: " + sale.Reference); Text("Customer: " + sale.Snapshot.CustomerName);
        Text("Business date: " + sale.Entries.First(x => x.Kind == "Invoice").BusinessDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        foreach (var (line, i) in sale.Snapshot.Lines.Select((line, i) => (line, i)))
        {
            if (y > 690) NewPage();
            Text($"{i + 1}. {line.Sku} | {line.Name} | {line.Mode}");
            Text($"Quantity: {line.Quantity.ToString(CultureInfo.InvariantCulture)} {line.Unit} | Unit price: {line.UnitPrice.ToString("0.######", CultureInfo.InvariantCulture)} PKR");
            Text($"Gross: {Money(line.Gross)} | Discount: {Money(line.Discount)} | Net: {Money(line.Net)}");
            if (!string.IsNullOrEmpty(line.CylinderReference)) Text("Cylinder: " + line.CylinderReference);
            y += 8;
        }
        Text("Total: " + Money(sale.Total)); Text("Paid / allocated: " + Money(sale.Paid)); Text("Balance: " + Money(sale.Outstanding));
        if (sale.LoanIds is { Length: > 0 })
        {
            Text("Refundable cylinder deposits collected separately: " + Money(sale.DepositCollected));
            Text("Deposits are not included in the sales total.");
            foreach (var line in sale.Snapshot.Lines.Where(x => x.Mode == "LoanRefill")) Text("Cylinder usage terms: " + line.UsageTerms);
            foreach (var loanId in sale.LoanIds) Text("Cylinder loan reference: " + loanId);
        }
        Text("Notes: " + sale.Snapshot.Notes);
        Text("Payment and correction history");
        foreach (var e in sale.Entries) Text($"{e.BusinessDate:yyyy-MM-dd} | {e.Kind} | Cash {Money(e.Cash)} | Balance movement {Money(e.Receivable)} | {e.Reason}");
        Text("Operational receipt. This is not a tax invoice.");
        graphics?.Dispose(); using var stream = new MemoryStream(); pdf.Save(stream, false); return stream.ToArray();
    }
}
