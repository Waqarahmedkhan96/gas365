using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SeparatePurchaseCosts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries");

        migrationBuilder.AddColumn<bool>(
            name: "SeparateCosts",
            table: "PurchaseDocuments",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries",
            sql: "(\"Kind\" = 'Invoice' AND \"Payable\" >= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'Payment' AND \"Payable\" < 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'PaymentReversal' AND \"Payable\" > 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'Reversal' AND \"Payable\" <= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'CostPayment' AND \"Payable\" = 0 AND \"Cash\" < 0) OR (\"Kind\" = 'CostPaymentReversal' AND \"Payable\" = 0 AND \"Cash\" > 0)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries");

        migrationBuilder.DropColumn(
            name: "SeparateCosts",
            table: "PurchaseDocuments");

        migrationBuilder.AddCheckConstraint(
            name: "CK_PurchaseEntry_Signs",
            table: "PurchaseEntries",
            sql: "(\"Kind\" = 'Invoice' AND \"Payable\" >= 0 AND \"Cash\" = 0) OR (\"Kind\" = 'Payment' AND \"Payable\" < 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'PaymentReversal' AND \"Payable\" > 0 AND \"Cash\" = \"Payable\") OR (\"Kind\" = 'Reversal' AND \"Payable\" <= 0 AND \"Cash\" = 0)");
    }
}
