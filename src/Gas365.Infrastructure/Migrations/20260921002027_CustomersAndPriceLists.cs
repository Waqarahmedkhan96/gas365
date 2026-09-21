using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class CustomersAndPriceLists : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['customers.view','customers.manage','prices.manage'])) WHERE "IsOwner" = TRUE;
            """);
        migrationBuilder.CreateTable(
            name: "Customers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Phone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Customers_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PriceLists",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PriceLists", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_PriceLists_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SalePrices",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                Mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SalePrices", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_SalePrice_Range", "\"UnitPrice\" >= 0 AND \"UnitPrice\" <= 1000000");
                table.ForeignKey(
                    name: "FK_SalePrices_PriceLists_TenantId_PriceListId",
                    columns: x => new { x.TenantId, x.PriceListId },
                    principalTable: "PriceLists",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SalePrices_StockItems_TenantId_ItemId",
                    columns: x => new { x.TenantId, x.ItemId },
                    principalTable: "StockItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SalePrices_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_TenantId_Name",
            table: "Customers",
            columns: new[] { "TenantId", "Name" });

        migrationBuilder.CreateIndex(
            name: "IX_PriceLists_TenantId_Name",
            table: "PriceLists",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SalePrices_TenantId_ItemId",
            table: "SalePrices",
            columns: new[] { "TenantId", "ItemId" });

        migrationBuilder.CreateIndex(
            name: "IX_SalePrices_TenantId_PriceListId_ItemId_Mode",
            table: "SalePrices",
            columns: new[] { "TenantId", "PriceListId", "ItemId", "Mode" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT p FROM unnest("Permissions") AS p WHERE p NOT IN ('customers.view','customers.manage','prices.manage'));
            """);
        migrationBuilder.DropTable(
            name: "Customers");

        migrationBuilder.DropTable(
            name: "SalePrices");

        migrationBuilder.DropTable(
            name: "PriceLists");
    }
}
