using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class PostedSales : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SaleDocuments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                StockDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Content = table.Column<string>(type: "jsonb", nullable: false),
                Total = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Cogs = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SaleDocuments", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_SaleDocuments_TenantId_BranchId_Id", x => new { x.TenantId, x.BranchId, x.Id });
                table.CheckConstraint("CK_SaleDocument_Amounts", "\"Total\" >= 0 AND \"Cogs\" >= 0");
                table.ForeignKey(
                    name: "FK_SaleDocuments_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SaleDocuments_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SaleDocuments_SaleDrafts_TenantId_DraftId",
                    columns: x => new { x.TenantId, x.DraftId },
                    principalTable: "SaleDrafts",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SaleDocuments_StockDocuments_TenantId_StockDocumentId",
                    columns: x => new { x.TenantId, x.StockDocumentId },
                    principalTable: "StockDocuments",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SaleEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Receivable = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Cash = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Revenue = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Cogs = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                ReversesId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SaleEntries", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_SaleEntry_Signs", "(\"Kind\" = 'Invoice' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" >= 0 AND \"Cash\" = 0 AND \"Cogs\" >= 0) OR (\"Kind\" = 'Payment' AND \"Cash\" > 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'PaymentReversal' AND \"Cash\" < 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'Reversal' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" <= 0 AND \"Cash\" = 0 AND \"Cogs\" <= 0)");
                table.ForeignKey(
                    name: "FK_SaleEntries_SaleDocuments_TenantId_BranchId_SaleId",
                    columns: x => new { x.TenantId, x.BranchId, x.SaleId },
                    principalTable: "SaleDocuments",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SaleEntries_SaleEntries_TenantId_ReversesId",
                    columns: x => new { x.TenantId, x.ReversesId },
                    principalTable: "SaleEntries",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_SaleDocuments_TenantId_CustomerId",
            table: "SaleDocuments",
            columns: new[] { "TenantId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_SaleDocuments_TenantId_DraftId",
            table: "SaleDocuments",
            columns: new[] { "TenantId", "DraftId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SaleDocuments_TenantId_StockDocumentId",
            table: "SaleDocuments",
            columns: new[] { "TenantId", "StockDocumentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SaleEntries_TenantId_BranchId_SaleId",
            table: "SaleEntries",
            columns: new[] { "TenantId", "BranchId", "SaleId" });

        migrationBuilder.CreateIndex(
            name: "IX_SaleEntries_TenantId_IdempotencyKey",
            table: "SaleEntries",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SaleEntries_TenantId_ReversesId",
            table: "SaleEntries",
            columns: new[] { "TenantId", "ReversesId" },
            unique: true,
            filter: "\"ReversesId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_SaleEntries_TenantId_Sequence",
            table: "SaleEntries",
            columns: new[] { "TenantId", "Sequence" },
            unique: true);
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['sales.post','sales.pay','sales.reverse','sales.view_profit'])) WHERE "IsOwner" = TRUE;
            CREATE TRIGGER sale_documents_immutable BEFORE UPDATE OR DELETE ON "SaleDocuments" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER sale_entries_immutable BEFORE UPDATE OR DELETE ON "SaleEntries" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE FUNCTION sale_draft_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "SaleDocuments" WHERE "TenantId" = OLD."TenantId" AND "DraftId" = OLD."Id") THEN
                    RAISE EXCEPTION 'Posted sales drafts are immutable' USING ERRCODE = '23514';
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER sale_drafts_guard BEFORE UPDATE OR DELETE ON "SaleDrafts" FOR EACH ROW EXECUTE FUNCTION sale_draft_guard();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION sale_draft_guard() CASCADE;
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT p FROM unnest("Permissions") AS p WHERE p NOT IN ('sales.post','sales.pay','sales.reverse','sales.view_profit'));
            """);
        migrationBuilder.DropTable(
            name: "SaleEntries");

        migrationBuilder.DropTable(
            name: "SaleDocuments");
    }
}
