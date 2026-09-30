using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class YearlyBilling : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RequestedBillingPeriod",
            table: "Tenants",
            type: "character varying(10)",
            maxLength: 10,
            nullable: true);

        migrationBuilder.AddCheckConstraint(
            name: "CK_Tenant_BillingPeriod",
            table: "Tenants",
            sql: "\"RequestedBillingPeriod\" IS NULL OR \"RequestedBillingPeriod\" IN ('Monthly','Yearly')");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_Tenant_BillingPeriod",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "RequestedBillingPeriod",
            table: "Tenants");
    }
}
