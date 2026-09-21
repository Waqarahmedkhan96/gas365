using Gas365.Application;

namespace Gas365.Api;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/workspace/sales").WithTags("Customers and prices").RequireAuthorization();
        g.MapGet("/drafts", (Guid branchId, ISalesDraftAccess s, CancellationToken ct, int page = 1) => s.DraftsAsync(branchId, page, ct));
        g.MapGet("/drafts/{id:guid}", (Guid id, ISalesDraftAccess s, CancellationToken ct) => s.DraftAsync(id, ct));
        g.MapPost("/drafts", (SaleDraftRequest r, ISalesDraftAccess s, CancellationToken ct) => s.SaveAsync(null, r, ct));
        g.MapPut("/drafts/{id:guid}", (Guid id, SaleDraftRequest r, ISalesDraftAccess s, CancellationToken ct) => s.SaveAsync(id, r, ct));
        g.MapGet("/customers", (ICustomerAccess s, CancellationToken ct, string? search = null, int page = 1) => s.CustomersAsync(search, page, ct));
        g.MapGet("/customers/{id:guid}", (Guid id, ICustomerAccess s, CancellationToken ct) => s.CustomerAsync(id, ct));
        g.MapPost("/customers", (CustomerRequest r, ICustomerAccess s, CancellationToken ct) => s.SaveCustomerAsync(null, r, ct));
        g.MapPut("/customers/{id:guid}", (Guid id, CustomerRequest r, ICustomerAccess s, CancellationToken ct) => s.SaveCustomerAsync(id, r, ct));
        g.MapGet("/price-lists", (ICustomerAccess s, CancellationToken ct, string? search = null, int page = 1) => s.PriceListsAsync(search, page, ct));
        g.MapPost("/price-lists", (PriceListRequest r, ICustomerAccess s, CancellationToken ct) => s.SavePriceListAsync(null, r, ct));
        g.MapPut("/price-lists/{id:guid}", (Guid id, PriceListRequest r, ICustomerAccess s, CancellationToken ct) => s.SavePriceListAsync(id, r, ct));
        g.MapGet("/price-lists/{id:guid}/prices", (Guid id, ICustomerAccess s, CancellationToken ct, int page = 1) => s.PricesAsync(id, page, ct));
        g.MapPost("/price-lists/{id:guid}/prices", (Guid id, SalePriceRequest r, ICustomerAccess s, CancellationToken ct) => s.SavePriceAsync(id, null, r, ct));
        g.MapPut("/price-lists/{id:guid}/prices/{priceId:guid}", (Guid id, Guid priceId, SalePriceRequest r, ICustomerAccess s, CancellationToken ct) => s.SavePriceAsync(id, priceId, r, ct));
    }
}
