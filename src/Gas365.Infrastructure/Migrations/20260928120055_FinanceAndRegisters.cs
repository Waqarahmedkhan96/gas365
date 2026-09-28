using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class FinanceAndRegisters : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_SaleEntry_Signs",
            table: "SaleEntries");

        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries");

        migrationBuilder.CreateTable(
            name: "ExpenseCategories",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Capital = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseCategories", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ExpenseCategories_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "MoneyAccounts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                DefaultCash = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_MoneyAccounts", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_MoneyAccounts_TenantId_BranchId_Id", x => new { x.TenantId, x.BranchId, x.Id });
                table.CheckConstraint("CK_MoneyAccount_Kind", "\"Kind\" IN ('Cash','Bank') AND (NOT \"DefaultCash\" OR \"Kind\" = 'Cash')");
                table.ForeignKey(
                    name: "FK_MoneyAccounts_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "FinanceEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                AccountId = table.Column<Guid>(type: "uuid", nullable: true),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                ReversesId = table.Column<Guid>(type: "uuid", nullable: true),
                SaleId = table.Column<Guid>(type: "uuid", nullable: true),
                PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Cash = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Counterparty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FinanceEntries", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_FinanceEntries_TenantId_BranchId_Id", x => new { x.TenantId, x.BranchId, x.Id });
                table.CheckConstraint("CK_FinanceEntry_Account", "(\"Cash\" = 0 OR \"AccountId\" IS NOT NULL) AND NOT (\"CustomerId\" IS NOT NULL AND \"SupplierId\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_FinanceEntries_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_ExpenseCategories_TenantId_CategoryId",
                    columns: x => new { x.TenantId, x.CategoryId },
                    principalTable: "ExpenseCategories",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_FinanceEntries_TenantId_BranchId_ParentId",
                    columns: x => new { x.TenantId, x.BranchId, x.ParentId },
                    principalTable: "FinanceEntries",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_FinanceEntries_TenantId_BranchId_ReversesId",
                    columns: x => new { x.TenantId, x.BranchId, x.ReversesId },
                    principalTable: "FinanceEntries",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_MoneyAccounts_TenantId_BranchId_AccountId",
                    columns: x => new { x.TenantId, x.BranchId, x.AccountId },
                    principalTable: "MoneyAccounts",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_PurchaseDocuments_TenantId_BranchId_Purchase~",
                    columns: x => new { x.TenantId, x.BranchId, x.PurchaseId },
                    principalTable: "PurchaseDocuments",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_SaleDocuments_TenantId_BranchId_SaleId",
                    columns: x => new { x.TenantId, x.BranchId, x.SaleId },
                    principalTable: "SaleDocuments",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_FinanceEntries_Suppliers_TenantId_SupplierId",
                    columns: x => new { x.TenantId, x.SupplierId },
                    principalTable: "Suppliers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "RegisterEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                Kind = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Opening = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Expected = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Counted = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RegisterEvents", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_RegisterEvent_Kind", "\"Kind\" IN ('Open','Close','Reopen') AND \"Counted\" >= 0");
                table.ForeignKey(
                    name: "FK_RegisterEvents_MoneyAccounts_TenantId_BranchId_AccountId",
                    columns: x => new { x.TenantId, x.BranchId, x.AccountId },
                    principalTable: "MoneyAccounts",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_SaleEntry_Signs",
            table: "SaleEntries",
            sql: "(\"Kind\" IN ('Allocation','AllocationReversal') AND \"Cash\" = 0 AND \"Revenue\" = 0 AND \"Cogs\" = 0 AND ((\"Kind\" = 'Allocation' AND \"Receivable\" < 0) OR (\"Kind\" = 'AllocationReversal' AND \"Receivable\" > 0))) OR (\"Kind\" = 'Invoice' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" >= 0 AND \"Cash\" = 0 AND \"Cogs\" >= 0) OR (\"Kind\" = 'Payment' AND \"Cash\" > 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'PaymentReversal' AND \"Cash\" < 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'Reversal' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" <= 0 AND \"Cash\" = 0 AND \"Cogs\" <= 0)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries",
            sql: "(\"Kind\" IN ('Allocation','AllocationReversal') AND \"Cash\" = 0 AND ((\"Kind\" = 'Allocation' AND \"Payable\" < 0) OR (\"Kind\" = 'AllocationReversal' AND \"Payable\" > 0))) OR (\"Kind\" = 'Invoice' AND \"Payable\" >= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'Payment' AND \"Payable\" < 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'PaymentReversal' AND \"Payable\" > 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'Reversal' AND \"Payable\" <= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'CostPayment' AND \"Payable\" = 0 AND \"Cash\" < 0) OR (\"Kind\" = 'CostPaymentReversal' AND \"Payable\" = 0 AND \"Cash\" > 0)");

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseCategories_TenantId_Name",
            table: "ExpenseCategories",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_AccountId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "AccountId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_ParentId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "ParentId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_PurchaseId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "PurchaseId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_ReversesId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "ReversesId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_SaleId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "SaleId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_Sequence",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "Sequence" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_CategoryId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "CategoryId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_CustomerId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_IdempotencyKey",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_ReversesId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "ReversesId" },
            unique: true,
            filter: "\"ReversesId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_SourceId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "SourceId" },
            unique: true,
            filter: "\"SourceId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_SupplierId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "SupplierId" });

        migrationBuilder.CreateIndex(
            name: "IX_MoneyAccounts_TenantId_BranchId",
            table: "MoneyAccounts",
            columns: new[] { "TenantId", "BranchId" },
            unique: true,
            filter: "\"DefaultCash\" = TRUE");

        migrationBuilder.CreateIndex(
            name: "IX_MoneyAccounts_TenantId_BranchId_Name",
            table: "MoneyAccounts",
            columns: new[] { "TenantId", "BranchId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_RegisterEvents_TenantId_AccountId_BusinessDate_Sequence",
            table: "RegisterEvents",
            columns: new[] { "TenantId", "AccountId", "BusinessDate", "Sequence" });

        migrationBuilder.CreateIndex(
            name: "IX_RegisterEvents_TenantId_BranchId_AccountId",
            table: "RegisterEvents",
            columns: new[] { "TenantId", "BranchId", "AccountId" });

        migrationBuilder.CreateIndex(
            name: "IX_RegisterEvents_TenantId_IdempotencyKey",
            table: "RegisterEvents",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);
        InstallGuards(migrationBuilder);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RemoveGuards(migrationBuilder);
        migrationBuilder.DropTable(
            name: "FinanceEntries");

        migrationBuilder.DropTable(
            name: "RegisterEvents");

        migrationBuilder.DropTable(
            name: "ExpenseCategories");

        migrationBuilder.DropTable(
            name: "MoneyAccounts");

        migrationBuilder.DropCheckConstraint(
            name: "CK_SaleEntry_Signs",
            table: "SaleEntries");

        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries");

        migrationBuilder.AddCheckConstraint(
            name: "CK_SaleEntry_Signs",
            table: "SaleEntries",
            sql: "(\"Kind\" = 'Invoice' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" >= 0 AND \"Cash\" = 0 AND \"Cogs\" >= 0) OR (\"Kind\" = 'Payment' AND \"Cash\" > 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'PaymentReversal' AND \"Cash\" < 0 AND \"Receivable\" = -\"Cash\" AND \"Revenue\" = 0 AND \"Cogs\" = 0) OR (\"Kind\" = 'Reversal' AND \"Receivable\" = \"Revenue\" AND \"Revenue\" <= 0 AND \"Cash\" = 0 AND \"Cogs\" <= 0)");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries",
            sql: "(\"Kind\" = 'Invoice' AND \"Payable\" >= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'Payment' AND \"Payable\" < 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'PaymentReversal' AND \"Payable\" > 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'Reversal' AND \"Payable\" <= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'CostPayment' AND \"Payable\" = 0 AND \"Cash\" < 0) OR (\"Kind\" = 'CostPaymentReversal' AND \"Payable\" = 0 AND \"Cash\" > 0)");
    }
}
