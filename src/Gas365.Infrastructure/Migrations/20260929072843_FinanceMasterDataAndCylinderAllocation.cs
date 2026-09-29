using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class FinanceMasterDataAndCylinderAllocation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CylinderLoanEntry_Reconcile",
            table: "CylinderLoanEntries");

        migrationBuilder.AddColumn<bool>(
            name: "Active",
            table: "MoneyAccounts",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<uint>(
            name: "xmin",
            table: "MoneyAccounts",
            type: "xid",
            rowVersion: true,
            nullable: false,
            defaultValue: 0u);

        migrationBuilder.AddColumn<Guid>(
            name: "CylinderLoanId",
            table: "FinanceEntries",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "Active",
            table: "ExpenseCategories",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<uint>(
            name: "xmin",
            table: "ExpenseCategories",
            type: "xid",
            rowVersion: true,
            nullable: false,
            defaultValue: 0u);

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_CylinderLoanId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "CylinderLoanId" });

        migrationBuilder.AddCheckConstraint(
            name: "CK_CylinderLoanEntry_Reconcile",
            table: "CylinderLoanEntries",
            sql: "(\"Kind\" IN ('Allocation','AllocationReversal') AND \"Cash\" = 0 AND \"Revenue\" = 0 AND \"Liability\" = 0 AND \"UsageFee\" = 0 AND \"DamageOrLossCharge\" = 0 AND ((\"Kind\" = 'Allocation' AND \"Receivable\" < 0) OR (\"Kind\" = 'AllocationReversal' AND \"Receivable\" > 0))) OR (\"Kind\" NOT IN ('Allocation','AllocationReversal') AND \"Cash\" + \"Receivable\" = \"Revenue\" + \"Liability\")");

        migrationBuilder.AddForeignKey(
            name: "FK_FinanceEntries_CylinderLoans_TenantId_BranchId_CylinderLoan~",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "CylinderLoanId" },
            principalTable: "CylinderLoans",
            principalColumns: new[] { "TenantId", "BranchId", "Id" },
            onDelete: ReferentialAction.Restrict);

        InstallGuards(migrationBuilder);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        RemoveGuards(migrationBuilder);

        migrationBuilder.DropForeignKey(
            name: "FK_FinanceEntries_CylinderLoans_TenantId_BranchId_CylinderLoan~",
            table: "FinanceEntries");

        migrationBuilder.DropIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_CylinderLoanId",
            table: "FinanceEntries");

        migrationBuilder.DropCheckConstraint(
            name: "CK_CylinderLoanEntry_Reconcile",
            table: "CylinderLoanEntries");

        migrationBuilder.DropColumn(
            name: "Active",
            table: "MoneyAccounts");

        migrationBuilder.DropColumn(
            name: "xmin",
            table: "MoneyAccounts");

        migrationBuilder.DropColumn(
            name: "CylinderLoanId",
            table: "FinanceEntries");

        migrationBuilder.DropColumn(
            name: "Active",
            table: "ExpenseCategories");

        migrationBuilder.DropColumn(
            name: "xmin",
            table: "ExpenseCategories");

        migrationBuilder.AddCheckConstraint(
            name: "CK_CylinderLoanEntry_Reconcile",
            table: "CylinderLoanEntries",
            sql: "\"Cash\" + \"Receivable\" = \"Revenue\" + \"Liability\"");
    }
}
