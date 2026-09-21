using Gas365.Application;

namespace Gas365.Api;

public static class PurchasingEndpoints
{
    public static void MapPurchasingEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/workspace/purchasing").WithTags("Purchasing").RequireAuthorization();
        g.MapGet("/suppliers", (IPurchasingAccess s, CancellationToken ct, string? search = null, int page = 1) => s.SuppliersAsync(search, page, ct));
        g.MapPost("/suppliers", (SupplierRequest r, IPurchasingAccess s, CancellationToken ct) => s.SaveSupplierAsync(null, r, ct));
        g.MapPut("/suppliers/{id:guid}", (Guid id, SupplierRequest r, IPurchasingAccess s, CancellationToken ct) => s.SaveSupplierAsync(id, r, ct));
        g.MapGet("/suppliers/{id:guid}", (Guid id, Guid branchId, IPurchasingAccess s, CancellationToken ct, int page = 1) => s.SupplierAsync(id, branchId, page, ct));
        g.MapGet("/purchases", (Guid branchId, IPurchasingAccess s, CancellationToken ct, int page = 1) => s.PurchasesAsync(branchId, page, ct));
        g.MapGet("/purchases/{id:guid}", (Guid id, IPurchasingAccess s, CancellationToken ct) => s.PurchaseAsync(id, ct));
        g.MapPost("/purchases", (PurchaseDraftRequest r, IPurchasingAccess s, CancellationToken ct) => s.SaveDraftAsync(null, r, ct));
        g.MapPut("/purchases/{id:guid}", (Guid id, PurchaseDraftRequest r, IPurchasingAccess s, CancellationToken ct) => s.SaveDraftAsync(id, r, ct));
        g.MapPost("/purchases/{id:guid}/post", (Guid id, PurchasePostRequest r, IPurchasingAccess s, CancellationToken ct) => s.PostAsync(id, r, ct));
        g.MapPost("/purchases/{id:guid}/payments", (Guid id, PurchasePaymentRequest r, IPurchasingAccess s, CancellationToken ct) => s.PayAsync(id, r, ct));
        g.MapPost("/purchases/{id:guid}/payments/{entryId:guid}/reverse", (Guid id, Guid entryId, PurchaseReverseRequest r, IPurchasingAccess s, CancellationToken ct) => s.ReversePaymentAsync(id, entryId, r, ct));
        g.MapPost("/purchases/{id:guid}/reverse", (Guid id, PurchaseReverseRequest r, IPurchasingAccess s, CancellationToken ct) => s.ReverseAsync(id, r, ct));
        g.MapGet("/purchases/{id:guid}/voucher", async (Guid id, IPurchasingAccess s, CancellationToken ct) => Results.File(await s.VoucherAsync(id, ct), "application/pdf", $"Gas365-purchase-{id}.pdf"));
    }
}
