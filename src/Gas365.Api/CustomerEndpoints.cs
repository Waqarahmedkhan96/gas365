using Gas365.Application;

namespace Gas365.Api;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/api/workspace/sales").WithTags("Customers and prices").RequireAuthorization();
        g.MapGet("/cylinder-policies/{id:guid}", async (Guid id, ICylinderLoanAccess s, CancellationToken ct) =>
        {
            var policy = await s.PolicyAsync(id, ct);
            return policy is null ? (IResult)Results.Content("null", "application/json") : Results.Json(policy);
        }).Produces<CylinderPolicyDto>();
        g.MapPut("/cylinder-policies/{id:guid}", (Guid id, CylinderPolicyRequest r, ICylinderLoanAccess s, CancellationToken ct) => s.SavePolicyAsync(id, r, ct));
        g.MapGet("/customers/{id:guid}/deposit-arrangement", (Guid id, ICylinderLoanAccess s, CancellationToken ct) => s.ArrangementAsync(id, ct));
        g.MapPut("/customers/{id:guid}/deposit-arrangement", (Guid id, DepositArrangementRequest r, ICylinderLoanAccess s, CancellationToken ct) => s.SaveArrangementAsync(id, r, ct));
        g.MapGet("/loans", (Guid branchId, ICylinderLoanAccess s, CancellationToken ct, Guid? customerId = null, int page = 1) => s.LoansAsync(branchId, customerId, page, ct));
        g.MapGet("/loans/{id:guid}", (Guid id, ICylinderLoanAccess s, CancellationToken ct) => s.LoanAsync(id, ct));
        g.MapPost("/loans/{id:guid}/approvals", (Guid id, LoanApprovalRequest r, ICylinderLoanAccess s, CancellationToken ct) => s.ApproveAsync(id, r, ct));
        g.MapPost("/loans/{id:guid}/return", (Guid id, LoanReturnRequest r, ICylinderLoanAccess s, CancellationToken ct) => s.ReturnAsync(id, r, ct));
        g.MapPost("/loans/{id:guid}/refunds", (Guid id, LoanPaymentRequest r, ICylinderLoanAccess s, CancellationToken ct) => s.RefundAsync(id, r, ct));
        g.MapPost("/loans/{id:guid}/payments", (Guid id, LoanPaymentRequest r, ICylinderLoanAccess s, CancellationToken ct) => s.PayAsync(id, r, ct));
        g.MapPost("/loans/{id:guid}/entries/{entryId:guid}/reverse", (Guid id, Guid entryId, SaleReverseRequest r, ICylinderLoanAccess s, CancellationToken ct) => s.ReverseAsync(id, entryId, r, ct));
        g.MapGet("/posted", (Guid branchId, ISalesAccess s, CancellationToken ct, Guid? customerId = null, int page = 1) => s.SalesAsync(branchId, customerId, page, ct));
        g.MapGet("/posted/{id:guid}", (Guid id, ISalesAccess s, CancellationToken ct) => s.SaleAsync(id, ct));
        g.MapGet("/posted/{id:guid}/receipt", async (Guid id, ISalesAccess s, HttpResponse response, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "no-store";
            return Results.File(await s.ReceiptAsync(id, ct), "application/pdf", $"Gas365-sale-{id}.pdf");
        });
        g.MapPost("/drafts/{id:guid}/post", (Guid id, SalePostRequest r, ISalesAccess s, CancellationToken ct) => s.PostAsync(id, r, ct));
        g.MapPost("/posted/{id:guid}/payments", (Guid id, SalePaymentRequest r, ISalesAccess s, CancellationToken ct) => s.PayAsync(id, r, ct));
        g.MapPost("/posted/{id:guid}/payments/{entryId:guid}/reverse", (Guid id, Guid entryId, SaleReverseRequest r, ISalesAccess s, CancellationToken ct) => s.ReversePaymentAsync(id, entryId, r, ct));
        g.MapPost("/posted/{id:guid}/reverse", (Guid id, SaleReverseRequest r, ISalesAccess s, CancellationToken ct) => s.ReverseAsync(id, r, ct));
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
