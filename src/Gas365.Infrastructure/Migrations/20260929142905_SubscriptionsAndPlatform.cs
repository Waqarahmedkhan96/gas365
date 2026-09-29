using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gas365.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SubscriptionsAndPlatform : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "CreatedAt",
            table: "Tenants",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "now()");

        migrationBuilder.AddColumn<bool>(
            name: "OnboardingDismissed",
            table: "Tenants",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "PaidThrough",
            table: "Tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PlanId",
            table: "Tenants",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RequestedPlanId",
            table: "Tenants",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SubscriptionStatus",
            table: "Tenants",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Active");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "TrialEndsAt",
            table: "Tenants",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SupportGrantId",
            table: "LoginSessions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Plans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                MonthlyPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                MaxBranches = table.Column<int>(type: "integer", nullable: false),
                MaxUsers = table.Column<int>(type: "integer", nullable: false),
                MaxAdmins = table.Column<int>(type: "integer", nullable: false),
                Active = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Plans", x => x.Id);
                table.CheckConstraint("CK_Plan_Limits", "\"MaxBranches\" > 0 AND \"MaxUsers\" > 0 AND \"MaxAdmins\" > 0 AND \"MaxAdmins\" <= \"MaxUsers\" AND (\"MonthlyPrice\" IS NULL OR \"MonthlyPrice\" >= 0)");
            });

        migrationBuilder.CreateTable(
            name: "SupportGrants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                GrantedBy = table.Column<Guid>(type: "uuid", nullable: false),
                IncludeFinance = table.Column<bool>(type: "boolean", nullable: false),
                Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                RevokedBy = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SupportGrants", x => new { x.TenantId, x.Id });
                table.CheckConstraint("CK_SupportGrant_Window", "\"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" <= \"CreatedAt\" + interval '72 hours'");
                table.ForeignKey(
                    name: "FK_SupportGrants_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "SubscriptionEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SubscriptionEvents", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_SubscriptionEvents_Plans_PlanId",
                    column: x => x.PlanId,
                    principalTable: "Plans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_SubscriptionEvents_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "TenantId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Tenants_PlanId",
            table: "Tenants",
            column: "PlanId");

        migrationBuilder.CreateIndex(
            name: "IX_Tenants_RequestedPlanId",
            table: "Tenants",
            column: "RequestedPlanId");

        migrationBuilder.AddCheckConstraint(
            name: "CK_Tenant_Subscription",
            table: "Tenants",
            sql: "\"SubscriptionStatus\" IN ('Trial','Active','Expired') AND (\"SubscriptionStatus\" <> 'Trial' OR \"TrialEndsAt\" IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "IX_Plans_Code",
            table: "Plans",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionEvents_PlanId",
            table: "SubscriptionEvents",
            column: "PlanId");

        migrationBuilder.CreateIndex(
            name: "IX_SubscriptionEvents_TenantId_OccurredAt",
            table: "SubscriptionEvents",
            columns: new[] { "TenantId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_SupportGrants_Id",
            table: "SupportGrants",
            column: "Id",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_SupportGrants_TenantId_ExpiresAt",
            table: "SupportGrants",
            columns: new[] { "TenantId", "ExpiresAt" });

        migrationBuilder.AddForeignKey(
            name: "FK_Tenants_Plans_PlanId",
            table: "Tenants",
            column: "PlanId",
            principalTable: "Plans",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Tenants_Plans_RequestedPlanId",
            table: "Tenants",
            column: "RequestedPlanId",
            principalTable: "Plans",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        // Owner-approved catalogue (2026-09-29); prices are set later by the platform administrator.
        // Existing businesses are pilot customers: grandfathered as active on the Business plan with no end date.
        migrationBuilder.Sql("""
            INSERT INTO "Plans" ("Id", "Code", "Name", "MonthlyPrice", "MaxBranches", "MaxUsers", "MaxAdmins", "Active", "SortOrder") VALUES
                ('7a1f0c2e-0000-4000-8000-000000000001', 'starter', 'Starter', NULL, 1, 3, 1, TRUE, 1),
                ('7a1f0c2e-0000-4000-8000-000000000002', 'business', 'Business', NULL, 3, 10, 3, TRUE, 2),
                ('7a1f0c2e-0000-4000-8000-000000000003', 'enterprise', 'Enterprise', NULL, 10, 50, 10, TRUE, 3);
            UPDATE "Tenants" SET "PlanId" = '7a1f0c2e-0000-4000-8000-000000000002', "SubscriptionStatus" = 'Active' WHERE "PlanId" IS NULL;
            CREATE TRIGGER subscription_events_immutable BEFORE UPDATE OR DELETE ON "SubscriptionEvents" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Tenants_Plans_PlanId",
            table: "Tenants");

        migrationBuilder.DropForeignKey(
            name: "FK_Tenants_Plans_RequestedPlanId",
            table: "Tenants");

        migrationBuilder.DropTable(
            name: "SubscriptionEvents");

        migrationBuilder.DropTable(
            name: "SupportGrants");

        migrationBuilder.DropTable(
            name: "Plans");

        migrationBuilder.DropIndex(
            name: "IX_Tenants_PlanId",
            table: "Tenants");

        migrationBuilder.DropIndex(
            name: "IX_Tenants_RequestedPlanId",
            table: "Tenants");

        migrationBuilder.DropCheckConstraint(
            name: "CK_Tenant_Subscription",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "OnboardingDismissed",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "PaidThrough",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "PlanId",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "RequestedPlanId",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "SubscriptionStatus",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "TrialEndsAt",
            table: "Tenants");

        migrationBuilder.DropColumn(
            name: "SupportGrantId",
            table: "LoginSessions");
    }
}
