using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class ReportingAndExports : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_StockDocuments_TenantId_BranchId",
            table: "StockDocuments");

        migrationBuilder.CreateTable(
            name: "ExportJobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReportKey = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                Filter = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                Error = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                FileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Content = table.Column<byte[]>(type: "bytea", nullable: true),
                RowCount = table.Column<int>(type: "integer", nullable: false),
                RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExportJobs", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_ExportJob_Format", "\"Format\" IN ('csv','xlsx','pdf')");
                table.CheckConstraint("CK_ExportJob_Status", "\"Status\" IN ('Queued','Running','Completed','Failed','Expired') AND \"Attempts\" >= 0 AND (\"Status\" <> 'Completed' OR \"Content\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_ExportJobs_Memberships_TenantId_MembershipId",
                    columns: x => new { x.TenantId, x.MembershipId },
                    principalTable: "Memberships",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StockDocuments_TenantId_BranchId_BusinessDate",
            table: "StockDocuments",
            columns: new[] { "TenantId", "BranchId", "BusinessDate" });

        migrationBuilder.CreateIndex(
            name: "IX_SaleEntries_TenantId_BranchId_BusinessDate",
            table: "SaleEntries",
            columns: new[] { "TenantId", "BranchId", "BusinessDate" });

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseEntries_TenantId_BranchId_BusinessDate",
            table: "PurchaseEntries",
            columns: new[] { "TenantId", "BranchId", "BusinessDate" });

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_BusinessDate",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "BusinessDate" });

        migrationBuilder.CreateIndex(
            name: "IX_CylinderLoanEntries_TenantId_BranchId_BusinessDate",
            table: "CylinderLoanEntries",
            columns: new[] { "TenantId", "BranchId", "BusinessDate" });

        migrationBuilder.CreateIndex(
            name: "IX_ExportJobs_Status_RequestedAt",
            table: "ExportJobs",
            columns: new[] { "Status", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ExportJobs_TenantId_IdempotencyKey",
            table: "ExportJobs",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ExportJobs_TenantId_MembershipId_RequestedAt",
            table: "ExportJobs",
            columns: new[] { "TenantId", "MembershipId", "RequestedAt" });

        // Owners receive reporting access; team roles need explicit assignment.
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['reports.view','reports.export'])) WHERE "IsOwner" = TRUE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = array_remove(array_remove("Permissions", 'reports.view'), 'reports.export');
            """);

        migrationBuilder.DropTable(
            name: "ExportJobs");

        migrationBuilder.DropIndex(
            name: "IX_StockDocuments_TenantId_BranchId_BusinessDate",
            table: "StockDocuments");

        migrationBuilder.DropIndex(
            name: "IX_SaleEntries_TenantId_BranchId_BusinessDate",
            table: "SaleEntries");

        migrationBuilder.DropIndex(
            name: "IX_PurchaseEntries_TenantId_BranchId_BusinessDate",
            table: "PurchaseEntries");

        migrationBuilder.DropIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_BusinessDate",
            table: "FinanceEntries");

        migrationBuilder.DropIndex(
            name: "IX_CylinderLoanEntries_TenantId_BranchId_BusinessDate",
            table: "CylinderLoanEntries");

        migrationBuilder.CreateIndex(
            name: "IX_StockDocuments_TenantId_BranchId",
            table: "StockDocuments",
            columns: new[] { "TenantId", "BranchId" });
    }
}
