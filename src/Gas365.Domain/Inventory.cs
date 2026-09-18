namespace Gas365.Domain;

public sealed class StockUnit : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Code { get; set; } = "";
    public int DecimalPlaces { get; set; }
}
public sealed class StockCategory : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
}
public sealed class StockItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "SparePart";
    public Guid UnitId { get; set; }
    public Guid? CategoryId { get; set; }
    public string Brand { get; set; } = "";
    public string Classification { get; set; } = "";
    public decimal? CapacityKg { get; set; }
    public decimal? TareWeightKg { get; set; }
    public bool SerialTracked { get; set; }
    public decimal ReorderLevel { get; set; }
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}
public sealed class Warehouse : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}

// Documents and their signed movements are immutable after posting.
public sealed class StockDocument : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ActorId { get; set; }
    public string Kind { get; set; } = "";
    public string Reason { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid? ReversesId { get; set; }
    public DateTimeOffset PostedAt { get; set; }
    public DateOnly BusinessDate { get; set; }
}
public sealed class StockMovement : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public long Sequence { get; set; }
    public Guid DocumentId { get; set; }
    public Guid BranchId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }
    public string Bucket { get; set; } = "Available";
    public decimal Quantity { get; set; }
    public decimal Value { get; set; }
    public decimal UnitCost { get; set; }
    public string[] Serials { get; set; } = [];
}
public sealed class CylinderAsset : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ItemId { get; set; }
    public string Serial { get; set; } = "";
    public string Owner { get; set; } = "";
    public string InspectionReference { get; set; } = "";
    public DateOnly? InspectionDue { get; set; }
    public uint Version { get; set; }
}
