using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class Purchasing : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Suppliers",
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
                table.PrimaryKey("PK_Suppliers", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Suppliers_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PurchaseDrafts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                Content = table.Column<string>(type: "jsonb", nullable: false),
                Snapshot = table.Column<string>(type: "jsonb", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PurchaseDrafts", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_PurchaseDrafts_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseDrafts_Suppliers_TenantId_SupplierId",
                    columns: x => new { x.TenantId, x.SupplierId },
                    principalTable: "Suppliers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseDrafts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PurchaseDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                StockDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                CorrectsId = table.Column<Guid>(type: "uuid", nullable: true),
                SupplierName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Content = table.Column<string>(type: "jsonb", nullable: false),
                Total = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PurchaseDocuments", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_PurchaseDocuments_TenantId_BranchId_Id", x => new { x.TenantId, x.BranchId, x.Id });
                table.ForeignKey(
                    name: "FK_PurchaseDocuments_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseDocuments_PurchaseDocuments_TenantId_CorrectsId",
                    columns: x => new { x.TenantId, x.CorrectsId },
                    principalTable: "PurchaseDocuments",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseDocuments_PurchaseDrafts_TenantId_DraftId",
                    columns: x => new { x.TenantId, x.DraftId },
                    principalTable: "PurchaseDrafts",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseDocuments_StockDocuments_TenantId_StockDocumentId",
                    columns: x => new { x.TenantId, x.StockDocumentId },
                    principalTable: "StockDocuments",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseDocuments_Suppliers_TenantId_SupplierId",
                    columns: x => new { x.TenantId, x.SupplierId },
                    principalTable: "Suppliers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseDocuments_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PurchaseEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                PurchaseId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                ReversesId = table.Column<Guid>(type: "uuid", nullable: true),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Payable = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Cash = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PurchaseEntries", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_PurchaseEntry_Signs", "(\"Kind\" = 'Invoice' AND \"Payable\" >= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'Payment' AND \"Payable\" < 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'PaymentReversal' AND \"Payable\" > 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'Reversal' AND \"Payable\" <= 0 AND \"Cash\" = 0)");
                table.ForeignKey(
                    name: "FK_PurchaseEntries_PurchaseDocuments_TenantId_BranchId_Purchas~",
                    columns: x => new { x.TenantId, x.BranchId, x.PurchaseId },
                    principalTable: "PurchaseDocuments",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseEntries_PurchaseEntries_TenantId_ReversesId",
                    columns: x => new { x.TenantId, x.ReversesId },
                    principalTable: "PurchaseEntries",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_PurchaseEntries_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseDocuments_TenantId_CorrectsId",
            table: "PurchaseDocuments",
            columns: new[] { "TenantId", "CorrectsId" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseDocuments_TenantId_DraftId",
            table: "PurchaseDocuments",
            columns: new[] { "TenantId", "DraftId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseDocuments_TenantId_StockDocumentId",
            table: "PurchaseDocuments",
            columns: new[] { "TenantId", "StockDocumentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseDocuments_TenantId_SupplierId",
            table: "PurchaseDocuments",
            columns: new[] { "TenantId", "SupplierId" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseDrafts_TenantId_BranchId",
            table: "PurchaseDrafts",
            columns: new[] { "TenantId", "BranchId" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseDrafts_TenantId_SupplierId",
            table: "PurchaseDrafts",
            columns: new[] { "TenantId", "SupplierId" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseEntries_TenantId_BranchId_PostedAt",
            table: "PurchaseEntries",
            columns: new[] { "TenantId", "BranchId", "PostedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseEntries_TenantId_BranchId_PurchaseId",
            table: "PurchaseEntries",
            columns: new[] { "TenantId", "BranchId", "PurchaseId" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseEntries_TenantId_IdempotencyKey",
            table: "PurchaseEntries",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseEntries_TenantId_ReversesId",
            table: "PurchaseEntries",
            columns: new[] { "TenantId", "ReversesId" },
            unique: true,
            filter: "\"ReversesId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_TenantId_Name",
            table: "Suppliers",
            columns: new[] { "TenantId", "Name" });
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['purchases.view_cost','purchases.create','purchases.post','purchases.pay','purchases.reverse','suppliers.manage'])) WHERE "IsOwner" = TRUE;
            CREATE TRIGGER purchase_documents_immutable BEFORE UPDATE OR DELETE ON "PurchaseDocuments" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER purchase_entries_immutable BEFORE UPDATE OR DELETE ON "PurchaseEntries" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE FUNCTION purchase_draft_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "PurchaseDocuments" WHERE "TenantId" = OLD."TenantId" AND "DraftId" = OLD."Id") THEN
                    RAISE EXCEPTION 'Posted purchase drafts are immutable' USING ERRCODE = '23514';
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER purchase_drafts_guard BEFORE UPDATE OR DELETE ON "PurchaseDrafts" FOR EACH ROW EXECUTE FUNCTION purchase_draft_guard();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION purchase_draft_guard() CASCADE;
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT p FROM unnest("Permissions") AS p WHERE p NOT IN ('purchases.view_cost','purchases.create','purchases.post','purchases.pay','purchases.reverse','suppliers.manage'));
            """);
        migrationBuilder.DropTable(
            name: "PurchaseEntries");

        migrationBuilder.DropTable(
            name: "PurchaseDocuments");

        migrationBuilder.DropTable(
            name: "PurchaseDrafts");

        migrationBuilder.DropTable(
            name: "Suppliers");
    }
}
