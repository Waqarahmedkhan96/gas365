namespace Gas365.Application;

public sealed record CustomerRequest(string Name, string Phone, string Address, bool Active = true, uint Version = 0);
public sealed record CustomerDto(Guid Id, string Name, string Phone, string Address, bool Active, uint Version);
public sealed record PriceListRequest(string Name, bool Active = true, uint Version = 0);
public sealed record PriceListDto(Guid Id, string Name, bool Active, uint Version);
public sealed record SalePriceRequest(Guid ItemId, string Mode, decimal UnitPrice, bool Active = true, uint Version = 0);
public sealed record SalePriceDto(Guid Id, Guid PriceListId, Guid ItemId, string Sku, string ItemName, string Unit, string Mode, decimal UnitPrice, bool Active, uint Version);
public interface ICustomerAccess
{
    Task<PageDto<CustomerDto>> CustomersAsync(string? search, int page, CancellationToken ct);
    Task<CustomerDto> CustomerAsync(Guid id, CancellationToken ct);
    Task<CustomerDto> SaveCustomerAsync(Guid? id, CustomerRequest request, CancellationToken ct);
    Task<PageDto<PriceListDto>> PriceListsAsync(string? search, int page, CancellationToken ct);
    Task<PriceListDto> SavePriceListAsync(Guid? id, PriceListRequest request, CancellationToken ct);
    Task<PageDto<SalePriceDto>> PricesAsync(Guid listId, int page, CancellationToken ct);
    Task<SalePriceDto> SavePriceAsync(Guid listId, Guid? id, SalePriceRequest request, CancellationToken ct);
}
