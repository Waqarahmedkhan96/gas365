namespace Gas365.Domain;

public sealed class Customer : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}

public sealed class PriceList : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}

public sealed class SalePrice : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PriceListId { get; set; }
    public Guid ItemId { get; set; }
    public string Mode { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public bool Active { get; set; } = true;
    public uint Version { get; set; }
}

public static class SaleModes
{
    public static bool Supports(string kind, string mode) => (kind, mode) switch
    {
        ("Lpg", "Refill") => true,
        ("Cylinder", "FullCylinder" or "CylinderOnly" or "Exchange" or "LoanRefill") => true,
        ("SparePart", "SparePart") => true,
        _ => false
    };
}
