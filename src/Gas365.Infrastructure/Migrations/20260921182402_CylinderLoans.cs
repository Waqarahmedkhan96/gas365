using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class CylinderLoans : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CustomerDepositArrangements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                ZeroDepositApproved = table.Column<bool>(type: "boolean", nullable: false),
                ApprovedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CustomerDepositArrangements", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_CustomerDepositArrangements_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CustomerDepositArrangements_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CylinderLoans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                SaleId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                Serial = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                Deposit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                UsageTerms = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CylinderLoans", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_CylinderLoans_TenantId_BranchId_Id", x => new { x.TenantId, x.BranchId, x.Id });
                table.CheckConstraint("CK_CylinderLoan_Deposit", "\"Deposit\" >= 0 AND \"Deposit\" <= 1000000000");
                table.ForeignKey(
                    name: "FK_CylinderLoans_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoans_SaleDocuments_TenantId_BranchId_SaleId",
                    columns: x => new { x.TenantId, x.BranchId, x.SaleId },
                    principalTable: "SaleDocuments",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoans_StockItems_TenantId_ItemId",
                    columns: x => new { x.TenantId, x.ItemId },
                    principalTable: "StockItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoans_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoans_Warehouses_TenantId_BranchId_WarehouseId",
                    columns: x => new { x.TenantId, x.BranchId, x.WarehouseId },
                    principalTable: "Warehouses",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CylinderPolicies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                GasItemId = table.Column<Guid>(type: "uuid", nullable: false),
                GasQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                SuggestedDeposit = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                UsageTerms = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CylinderPolicies", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_CylinderPolicy_Amounts", "\"GasQuantity\" > 0 AND \"GasQuantity\" <= 1000000 AND \"SuggestedDeposit\" >= 0 AND \"SuggestedDeposit\" <= 1000000000");
                table.ForeignKey(
                    name: "FK_CylinderPolicies_StockItems_TenantId_GasItemId",
                    columns: x => new { x.TenantId, x.GasItemId },
                    principalTable: "StockItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderPolicies_StockItems_TenantId_ItemId",
                    columns: x => new { x.TenantId, x.ItemId },
                    principalTable: "StockItems",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderPolicies_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "CylinderLoanEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                LoanId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Cash = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Liability = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Receivable = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Revenue = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                UsageFee = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                DamageOrLossCharge = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Condition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                StockDocumentId = table.Column<Guid>(type: "uuid", nullable: true),
                ApprovalId = table.Column<Guid>(type: "uuid", nullable: true),
                ReversesId = table.Column<Guid>(type: "uuid", nullable: true),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CylinderLoanEntries", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_CylinderLoanEntry_Reconcile", "\"Cash\" + \"Receivable\" = \"Revenue\" + \"Liability\"");
                table.ForeignKey(
                    name: "FK_CylinderLoanEntries_CylinderLoanEntries_TenantId_ApprovalId",
                    columns: x => new { x.TenantId, x.ApprovalId },
                    principalTable: "CylinderLoanEntries",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoanEntries_CylinderLoanEntries_TenantId_ReversesId",
                    columns: x => new { x.TenantId, x.ReversesId },
                    principalTable: "CylinderLoanEntries",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoanEntries_CylinderLoans_TenantId_BranchId_LoanId",
                    columns: x => new { x.TenantId, x.BranchId, x.LoanId },
                    principalTable: "CylinderLoans",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoanEntries_StockDocuments_TenantId_StockDocumentId",
                    columns: x => new { x.TenantId, x.StockDocumentId },
                    principalTable: "StockDocuments",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_CylinderLoanEntries_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_CustomerDepositArrangements_TenantId_CustomerId",
            table: "CustomerDepositArrangements",
            columns: new[] { "TenantId", "CustomerId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoanEntries_TenantId_ApprovalId",
            table: "CylinderLoanEntries",
            columns: new[] { "TenantId", "ApprovalId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoanEntries_TenantId_BranchId_LoanId",
            table: "CylinderLoanEntries",
            columns: new[] { "TenantId", "BranchId", "LoanId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoanEntries_TenantId_IdempotencyKey",
            table: "CylinderLoanEntries",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoanEntries_TenantId_ReversesId",
            table: "CylinderLoanEntries",
            columns: new[] { "TenantId", "ReversesId" },
            unique: true,
            filter: "\"ReversesId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoanEntries_TenantId_Sequence",
            table: "CylinderLoanEntries",
            columns: new[] { "TenantId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoanEntries_TenantId_StockDocumentId",
            table: "CylinderLoanEntries",
            columns: new[] { "TenantId", "StockDocumentId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoans_TenantId_BranchId_SaleId",
            table: "CylinderLoans",
            columns: new[] { "TenantId", "BranchId", "SaleId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoans_TenantId_BranchId_WarehouseId",
            table: "CylinderLoans",
            columns: new[] { "TenantId", "BranchId", "WarehouseId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoans_TenantId_CustomerId",
            table: "CylinderLoans",
            columns: new[] { "TenantId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoans_TenantId_ItemId",
            table: "CylinderLoans",
            columns: new[] { "TenantId", "ItemId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderPolicies_TenantId_GasItemId",
            table: "CylinderPolicies",
            columns: new[] { "TenantId", "GasItemId" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderPolicies_TenantId_ItemId",
            table: "CylinderPolicies",
            columns: new[] { "TenantId", "ItemId" },
            unique: true);
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['cylinders.configure','cylinders.waive_deposit','cylinders.return','cylinders.approve_damage_loss'])) WHERE "IsOwner" = TRUE;
            CREATE TRIGGER cylinder_loans_immutable BEFORE UPDATE OR DELETE ON "CylinderLoans" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER cylinder_loan_entries_immutable BEFORE UPDATE OR DELETE ON "CylinderLoanEntries" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT p FROM unnest("Permissions") AS p WHERE p NOT IN ('cylinders.configure','cylinders.waive_deposit','cylinders.return','cylinders.approve_damage_loss'));
            """);
        migrationBuilder.DropTable(
            name: "CustomerDepositArrangements");

        migrationBuilder.DropTable(
            name: "CylinderLoanEntries");

        migrationBuilder.DropTable(
            name: "CylinderPolicies");

        migrationBuilder.DropTable(
            name: "CylinderLoans");
    }
}
