using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AuditImportsAndNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<TimeOnly>(
            name: "ClosingReminder",
            table: "Tenants",
            type: "time without time zone",
            nullable: false,
            defaultValue: new TimeOnly(21, 0, 0));

        migrationBuilder.AddColumn<int>(
            name: "OverdueCreditDays",
            table: "Tenants",
            type: "integer",
            nullable: false,
            defaultValue: 30);

        migrationBuilder.AddColumn<Guid>(
            name: "OpeningBalanceId",
            table: "FinanceEntries",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "ImportBatches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                MembershipId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                FileName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Headers = table.Column<string>(type: "text", nullable: false),
                Mapping = table.Column<string>(type: "text", nullable: false),
                RowCount = table.Column<int>(type: "integer", nullable: false),
                ErrorCount = table.Column<int>(type: "integer", nullable: false),
                CommittedCount = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportBatches", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ImportBatches_Memberships_TenantId_MembershipId",
                    columns: x => new { x.TenantId, x.MembershipId },
                    principalTable: "Memberships",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "JobStates",
            columns: table => new
            {
                Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                LastStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastSucceededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastFailedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JobStates", x => x.Name);
            });

        migrationBuilder.CreateTable(
            name: "Notifications",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Permission = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Body = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Notifications", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Notifications_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OpeningBalances",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                OriginalDate = table.Column<DateOnly>(type: "date", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpeningBalances", x => new { x.TenantId, x.Id });
                table.UniqueConstraint("AK_OpeningBalances_TenantId_BranchId_Id", x => new { x.TenantId, x.BranchId, x.Id });
                table.CheckConstraint("CK_OpeningBalance_Party", "(\"CustomerId\" IS NULL) <> (\"SupplierId\" IS NULL) AND \"Amount\" > 0");
                table.ForeignKey(
                    name: "FK_OpeningBalances_Branches_TenantId_BranchId",
                    columns: x => new { x.TenantId, x.BranchId },
                    principalTable: "Branches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OpeningBalances_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OpeningBalances_Suppliers_TenantId_SupplierId",
                    columns: x => new { x.TenantId, x.SupplierId },
                    principalTable: "Suppliers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ImportRows",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                RowNumber = table.Column<int>(type: "integer", nullable: false),
                Data = table.Column<string>(type: "text", nullable: false),
                Errors = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ResultId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportRows", x => new { x.TenantId, x.BatchId, x.RowNumber });
                table.ForeignKey(
                    name: "FK_ImportRows_ImportBatches_TenantId_BatchId",
                    columns: x => new { x.TenantId, x.BatchId },
                    principalTable: "ImportBatches",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "NotificationReads",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                NotificationId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_NotificationReads", x => new { x.TenantId, x.NotificationId, x.UserId });
                table.ForeignKey(
                    name: "FK_NotificationReads_Notifications_TenantId_NotificationId",
                    columns: x => new { x.TenantId, x.NotificationId },
                    principalTable: "Notifications",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OpeningBalanceEntries",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                OpeningBalanceId = table.Column<Guid>(type: "uuid", nullable: false),
                Sequence = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(20,2)", precision: 20, scale: 2, nullable: false),
                ReversesId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpeningBalanceEntries", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_OpeningBalanceEntry_Signs", "(\"Kind\" = 'Opening' AND \"Amount\" > 0) OR (\"Kind\" IN ('Allocation','Reversal') AND \"Amount\" < 0) OR (\"Kind\" = 'AllocationReversal' AND \"Amount\" > 0)");
                table.ForeignKey(
                    name: "FK_OpeningBalanceEntries_OpeningBalanceEntries_TenantId_Revers~",
                    columns: x => new { x.TenantId, x.ReversesId },
                    principalTable: "OpeningBalanceEntries",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OpeningBalanceEntries_OpeningBalances_TenantId_BranchId_Ope~",
                    columns: x => new { x.TenantId, x.BranchId, x.OpeningBalanceId },
                    principalTable: "OpeningBalances",
                    principalColumns: new[] { "TenantId", "BranchId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddCheckConstraint(
            name: "CK_Tenant_Notifications",
            table: "Tenants",
            sql: "\"OverdueCreditDays\" BETWEEN 1 AND 365");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_OpeningBalanceId",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "OpeningBalanceId" });

        migrationBuilder.CreateIndex(
            name: "IX_ImportBatches_TenantId_CreatedAt",
            table: "ImportBatches",
            columns: new[] { "TenantId", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ImportBatches_TenantId_MembershipId",
            table: "ImportBatches",
            columns: new[] { "TenantId", "MembershipId" });

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_TenantId_Key",
            table: "Notifications",
            columns: new[] { "TenantId", "Key" },
            unique: true,
            filter: "\"ResolvedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Notifications_TenantId_ResolvedAt_CreatedAt",
            table: "Notifications",
            columns: new[] { "TenantId", "ResolvedAt", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalanceEntries_TenantId_BranchId_BusinessDate",
            table: "OpeningBalanceEntries",
            columns: new[] { "TenantId", "BranchId", "BusinessDate" });

        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalanceEntries_TenantId_BranchId_OpeningBalanceId",
            table: "OpeningBalanceEntries",
            columns: new[] { "TenantId", "BranchId", "OpeningBalanceId" });

        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalanceEntries_TenantId_IdempotencyKey",
            table: "OpeningBalanceEntries",
            columns: new[] { "TenantId", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalanceEntries_TenantId_OpeningBalanceId_Sequence",
            table: "OpeningBalanceEntries",
            columns: new[] { "TenantId", "OpeningBalanceId", "Sequence" });

        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalanceEntries_TenantId_ReversesId",
            table: "OpeningBalanceEntries",
            columns: new[] { "TenantId", "ReversesId" },
            unique: true,
            filter: "\"ReversesId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalances_TenantId_CustomerId",
            table: "OpeningBalances",
            columns: new[] { "TenantId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_OpeningBalances_TenantId_SupplierId",
            table: "OpeningBalances",
            columns: new[] { "TenantId", "SupplierId" });

        migrationBuilder.AddForeignKey(
            name: "FK_FinanceEntries_OpeningBalances_TenantId_BranchId_OpeningBal~",
            table: "FinanceEntries",
            columns: new[] { "TenantId", "BranchId", "OpeningBalanceId" },
            principalTable: "OpeningBalances",
            principalColumns: new[] { "TenantId", "BranchId", "Id" },
            onDelete: ReferentialAction.Restrict);

        // Opening balances are posted records: append-only like every other ledger. Owners may import.
        migrationBuilder.Sql("""
            CREATE TRIGGER opening_balances_immutable BEFORE UPDATE OR DELETE ON "OpeningBalances" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER opening_balance_entries_immutable BEFORE UPDATE OR DELETE ON "OpeningBalanceEntries" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['imports.manage'])) WHERE "IsOwner" = TRUE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = array_remove("Permissions", 'imports.manage');
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_FinanceEntries_OpeningBalances_TenantId_BranchId_OpeningBal~",
            table: "FinanceEntries");

        migrationBuilder.DropTable(
            name: "ImportRows");

        migrationBuilder.DropTable(
            name: "JobStates");

        migrationBuilder.DropTable(
            name: "NotificationReads");

        migrationBuilder.DropTable(
            name: "OpeningBalanceEntries");

        migrationBuilder.DropTable(
            name: "ImportBatches");

        migrationBuilder.DropTable(
            name: "Notifications");

        migrationBuilder.DropTable(
            name: "OpeningBalances");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Tenant_Notifications",
            table: "Tenants");

        migrationBuilder.DropIndex(
            name: "IX_FinanceEntries_TenantId_BranchId_OpeningBalanceId",
            table: "FinanceEntries");

        migrationBuilder.DropColumn(
            name: "ClosingReminder",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "OverdueCreditDays",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "OpeningBalanceId",
            table: "FinanceEntries");
    }
}
