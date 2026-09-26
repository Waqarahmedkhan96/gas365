using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SalesDrafts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['sales.view','sales.create','sales.discount'])) WHERE "IsOwner" = TRUE;
            """);
        migrationBuilder.CreateTable(
            name: "SaleDrafts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                PriceListId = table.Column<Guid>(type: "uuid", nullable: false),
                Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Content = table.Column<string>(type: "jsonb", nullable: false),
                Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SaleDrafts", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_SaleDrafts_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SaleDrafts_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SaleDrafts_PriceLists_TenantId_PriceListId",
                    columns: x => new { x.TenantId, x.PriceListId },
                    principalTable: "PriceLists",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SaleDrafts_TenantId_BranchId_Reference",
            table: "SaleDrafts",
            columns: new[] { "TenantId", "BranchId", "Reference" });

        migrationBuilder.CreateIndex(
            name: "IX_SaleDrafts_TenantId_CustomerId",
            table: "SaleDrafts",
            columns: new[] { "TenantId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_SaleDrafts_TenantId_PriceListId",
            table: "SaleDrafts",
            columns: new[] { "TenantId", "PriceListId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT p FROM unnest("Permissions") AS p WHERE p NOT IN ('sales.view','sales.create','sales.discount'));
            """);
        migrationBuilder.DropTable(
            name: "SaleDrafts");
    }
}
