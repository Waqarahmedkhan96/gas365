using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InventoryLedger : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "StockCategories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockCategories", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_StockCategories_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StockDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ReversesId = table.Column<Guid>(type: "uuid", nullable: true),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockDocuments", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_StockDocuments_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StockDocuments_StockDocuments_TenantId_ReversesId",
                    columns: x => new { x.TenantId, x.ReversesId },
                    principalTable: "StockDocuments",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StockDocuments_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StockUnits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                DecimalPlaces = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockUnits", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_Unit_Precision", "\"DecimalPlaces\" BETWEEN 0 AND 3");
                table.ForeignKey(
                    name: "FK_StockUnits_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "Warehouses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Warehouses", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_Warehouses_TenantId_BranchId_Id", x => new { x.TenantId, x.BranchId, x.Id });
                table.ForeignKey(
                    name: "FK_Warehouses_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_Warehouses_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StockItems",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Sku = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Classification = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CapacityKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                TareWeightKg = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: true),
                SerialTracked = table.Column<bool>(type: "boolean", nullable: false),
                ReorderLevel = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockItems", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_Item_Kind", "\"Kind\" IN ('Lpg','Cylinder','SparePart') AND \"ReorderLevel\" >= 0 AND (NOT \"SerialTracked\" OR \"Kind\" = 'Cylinder')");
                table.ForeignKey(
                    name: "FK_StockItems_StockCategories_TenantId_CategoryId",
                    columns: x => new { x.TenantId, x.CategoryId },
                    principalTable: "StockCategories",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StockItems_StockUnits_TenantId_UnitId",
                    columns: x => new { x.TenantId, x.UnitId },
                    principalTable: "StockUnits",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StockItems_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CylinderAssets",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                Serial = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Owner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                InspectionReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                InspectionDue = table.Column<DateOnly>(type: "date", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CylinderAssets", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_CylinderAssets_StockItems_TenantId_ItemId",
                    columns: x => new { x.TenantId, x.ItemId },
                    principalTable: "StockItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderAssets_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "StockMovements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                Bucket = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                Value = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                UnitCost = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                Serials = table.Column<string[]>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockMovements", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_Movement_Signs", "\"Quantity\" <> 0 AND \"UnitCost\" >= 0 AND ((\"Quantity\" > 0 AND \"Value\" >= 0) OR (\"Quantity\" < 0 AND \"Value\" <= 0))");
                table.ForeignKey(
                    name: "FK_StockMovements_StockDocuments_TenantId_DocumentId",
                    columns: x => new { x.TenantId, x.DocumentId },
                    principalTable: "StockDocuments",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StockMovements_StockItems_TenantId_ItemId",
                    columns: x => new { x.TenantId, x.ItemId },
                    principalTable: "StockItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StockMovements_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_StockMovements_Warehouses_TenantId_BranchId_WarehouseId",
                    columns: x => new { x.TenantId, x.BranchId, x.WarehouseId },
                    principalTable: "Warehouses",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderAssets_TenantId_ItemId",
            table: "CylinderAssets",
            columns: new[] { "TenantId", "ItemId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderAssets_TenantId_Serial",
            table: "CylinderAssets",
            columns: new[] { "TenantId", "Serial" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StockCategories_TenantId_Name",
            table: "StockCategories",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StockDocuments_TenantId_BranchId",
            table: "StockDocuments",
            columns: new[] { "TenantId", "BranchId" });

        migrationBuilder.CreateIndex(
            name: "IX_StockDocuments_TenantId_IdempotencyKey",
            table: "StockDocuments",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StockDocuments_TenantId_ReversesId",
            table: "StockDocuments",
            columns: new[] { "TenantId", "ReversesId" },
            unique: true,
            filter: "\"ReversesId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_StockItems_TenantId_CategoryId",
            table: "StockItems",
            columns: new[] { "TenantId", "CategoryId" });

        migrationBuilder.CreateIndex(
            name: "IX_StockItems_TenantId_Sku",
            table: "StockItems",
            columns: new[] { "TenantId", "Sku" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StockItems_TenantId_UnitId",
            table: "StockItems",
            columns: new[] { "TenantId", "UnitId" });

        migrationBuilder.CreateIndex(
            name: "IX_StockMovements_TenantId_BranchId_WarehouseId",
            table: "StockMovements",
            columns: new[] { "TenantId", "BranchId", "WarehouseId" });

        migrationBuilder.CreateIndex(
            name: "IX_StockMovements_TenantId_DocumentId",
            table: "StockMovements",
            columns: new[] { "TenantId", "DocumentId" });

        migrationBuilder.CreateIndex(
            name: "IX_StockMovements_TenantId_ItemId_WarehouseId_Sequence",
            table: "StockMovements",
            columns: new[] { "TenantId", "ItemId", "WarehouseId", "Sequence" });

        migrationBuilder.CreateIndex(
            name: "IX_StockMovements_TenantId_Sequence",
            table: "StockMovements",
            columns: new[] { "TenantId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StockUnits_TenantId_Code",
            table: "StockUnits",
            columns: new[] { "TenantId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Warehouses_TenantId_BranchId_Name",
            table: "Warehouses",
            columns: new[] { "TenantId", "BranchId", "Name" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CylinderAssets");

        migrationBuilder.DropTable(
            name: "StockMovements");

        migrationBuilder.DropTable(
            name: "StockDocuments");

        migrationBuilder.DropTable(
            name: "StockItems");

        migrationBuilder.DropTable(
            name: "Warehouses");

        migrationBuilder.DropTable(
            name: "StockCategories");

        migrationBuilder.DropTable(
            name: "StockUnits");
    }
}
