using Microsoft.EntityFrameworkCore.Migrations;

namespace Gas365.Infrastructure.Migrations;

public partial class FinanceMasterDataAndCylinderAllocation
{
    // Accounts and categories become renamable/retirable master data. Identity and classification stay fixed so
    // posted history is never reinterpreted, and rows are still never deleted.
    private static void InstallGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER money_accounts_immutable ON "MoneyAccounts";
            DROP TRIGGER expense_categories_immutable ON "ExpenseCategories";
            CREATE FUNCTION finance_master_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Finance master records cannot be deleted' USING ERRCODE = '23514'; END IF;
              IF TG_TABLE_NAME = 'MoneyAccounts' THEN
                IF (NEW."Id", NEW."TenantId", NEW."BranchId", NEW."Kind", NEW."DefaultCash", NEW."CreatedAt") IS DISTINCT FROM (OLD."Id", OLD."TenantId", OLD."BranchId", OLD."Kind", OLD."DefaultCash", OLD."CreatedAt")
                  THEN RAISE EXCEPTION 'Account identity is immutable' USING ERRCODE = '23514'; END IF;
                IF NEW."DefaultCash" AND NOT NEW."Active" THEN RAISE EXCEPTION 'The default cash drawer cannot be retired' USING ERRCODE = '23514'; END IF;
              ELSIF (NEW."Id", NEW."TenantId", NEW."Capital") IS DISTINCT FROM (OLD."Id", OLD."TenantId", OLD."Capital") THEN
                RAISE EXCEPTION 'Category identity is immutable' USING ERRCODE = '23514';
              END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER money_accounts_guard BEFORE UPDATE OR DELETE ON "MoneyAccounts" FOR EACH ROW EXECUTE FUNCTION finance_master_guard();
            CREATE TRIGGER expense_categories_guard BEFORE UPDATE OR DELETE ON "ExpenseCategories" FOR EACH ROW EXECUTE FUNCTION finance_master_guard();
            CREATE OR REPLACE FUNCTION finance_cash_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE account_kind text; account_active boolean; session_kind text; default_account uuid;
            BEGIN
              PERFORM 1 FROM "Tenants" WHERE "TenantId" = NEW."TenantId" FOR UPDATE;
              SELECT "Id" INTO default_account FROM "MoneyAccounts" WHERE "TenantId" = NEW."TenantId" AND "BranchId" = NEW."BranchId" AND "DefaultCash";
              SELECT "Kind" INTO session_kind FROM "RegisterEvents" WHERE "TenantId" = NEW."TenantId" AND "AccountId" = default_account AND "BusinessDate" = NEW."BusinessDate" ORDER BY "Sequence" DESC LIMIT 1;
              IF session_kind = 'Close' THEN RAISE EXCEPTION 'Closed branch day requires reopening' USING ERRCODE = '23514'; END IF;
              IF NEW."AccountId" IS NOT NULL AND NEW."Kind" <> 'Opening' THEN
                SELECT "Kind", "Active" INTO account_kind, account_active FROM "MoneyAccounts" WHERE "TenantId" = NEW."TenantId" AND "BranchId" = NEW."BranchId" AND "Id" = NEW."AccountId";
                IF NOT account_active THEN RAISE EXCEPTION 'Account is retired' USING ERRCODE = '23514'; END IF;
                IF account_kind = 'Cash' THEN
                  SELECT "Kind" INTO session_kind FROM "RegisterEvents" WHERE "TenantId" = NEW."TenantId" AND "AccountId" = NEW."AccountId" AND "BusinessDate" = NEW."BusinessDate" ORDER BY "Sequence" DESC LIMIT 1;
                  IF session_kind IS NULL OR session_kind = 'Close' THEN RAISE EXCEPTION 'Cash register is not open' USING ERRCODE = '23514'; END IF;
                END IF;
              END IF;
              RETURN NEW;
            END $$;
            """);
    }
    private static void RemoveGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER money_accounts_guard ON "MoneyAccounts";
            DROP TRIGGER expense_categories_guard ON "ExpenseCategories";
            DROP FUNCTION finance_master_guard();
            CREATE TRIGGER money_accounts_immutable BEFORE UPDATE OR DELETE ON "MoneyAccounts" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER expense_categories_immutable BEFORE UPDATE OR DELETE ON "ExpenseCategories" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE OR REPLACE FUNCTION finance_cash_guard() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE account_kind text; session_kind text; default_account uuid;
            BEGIN
              PERFORM 1 FROM "Tenants" WHERE "TenantId" = NEW."TenantId" FOR UPDATE;
              SELECT "Id" INTO default_account FROM "MoneyAccounts" WHERE "TenantId" = NEW."TenantId" AND "BranchId" = NEW."BranchId" AND "DefaultCash";
              SELECT "Kind" INTO session_kind FROM "RegisterEvents" WHERE "TenantId" = NEW."TenantId" AND "AccountId" = default_account AND "BusinessDate" = NEW."BusinessDate" ORDER BY "Sequence" DESC LIMIT 1;
              IF session_kind = 'Close' THEN RAISE EXCEPTION 'Closed branch day requires reopening' USING ERRCODE = '23514'; END IF;
              IF NEW."AccountId" IS NOT NULL AND NEW."Kind" <> 'Opening' THEN
                SELECT "Kind" INTO account_kind FROM "MoneyAccounts" WHERE "TenantId" = NEW."TenantId" AND "BranchId" = NEW."BranchId" AND "Id" = NEW."AccountId";
                IF account_kind = 'Cash' THEN
                  SELECT "Kind" INTO session_kind FROM "RegisterEvents" WHERE "TenantId" = NEW."TenantId" AND "AccountId" = NEW."AccountId" AND "BusinessDate" = NEW."BusinessDate" ORDER BY "Sequence" DESC LIMIT 1;
                  IF session_kind IS NULL OR session_kind = 'Close' THEN RAISE EXCEPTION 'Cash register is not open' USING ERRCODE = '23514'; END IF;
                END IF;
              END IF;
              RETURN NEW;
            END $$;
            """);
    }
}
