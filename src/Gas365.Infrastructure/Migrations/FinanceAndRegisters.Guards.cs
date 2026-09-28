using Microsoft.EntityFrameworkCore.Migrations;

namespace Gas365.Infrastructure.Migrations;

public partial class FinanceAndRegisters
{
    private static void InstallGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['finance.view','finance.post','finance.manage','finance.reverse','register.close','register.reopen'])) WHERE "IsOwner" = TRUE;
            CREATE TRIGGER finance_entries_immutable BEFORE UPDATE OR DELETE ON "FinanceEntries" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER money_accounts_immutable BEFORE UPDATE OR DELETE ON "MoneyAccounts" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER expense_categories_immutable BEFORE UPDATE OR DELETE ON "ExpenseCategories" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER register_events_immutable BEFORE UPDATE OR DELETE ON "RegisterEvents" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE FUNCTION finance_cash_guard() RETURNS trigger LANGUAGE plpgsql AS $$
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
            CREATE TRIGGER finance_cash_check BEFORE INSERT ON "FinanceEntries" FOR EACH ROW EXECUTE FUNCTION finance_cash_guard();
            CREATE FUNCTION finance_capture_legacy() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE account_id uuid; session_kind text;
            BEGIN
              PERFORM 1 FROM "Tenants" WHERE "TenantId" = NEW."TenantId" FOR UPDATE;
              SELECT "Id" INTO account_id FROM "MoneyAccounts" WHERE "TenantId" = NEW."TenantId" AND "BranchId" = NEW."BranchId" AND "DefaultCash";
              IF account_id IS NULL THEN RETURN NEW; END IF;
              SELECT "Kind" INTO session_kind FROM "RegisterEvents" WHERE "TenantId" = NEW."TenantId" AND "AccountId" = account_id AND "BusinessDate" = NEW."BusinessDate" ORDER BY "Sequence" DESC LIMIT 1;
              IF session_kind = 'Close' THEN RAISE EXCEPTION 'Closed business day requires reopening' USING ERRCODE = '23514'; END IF;
              IF NEW."Cash" <> 0 THEN
                INSERT INTO "FinanceEntries" ("Id","TenantId","BranchId","AccountId","SourceId","Kind","Amount","Cash","Counterparty","Reason","IdempotencyKey","RequestHash","ActorId","PostedAt","BusinessDate")
                VALUES (gen_random_uuid(),NEW."TenantId",NEW."BranchId",account_id,NEW."Id",CASE TG_TABLE_NAME WHEN 'SaleEntries' THEN 'SaleCash' WHEN 'PurchaseEntries' THEN 'PurchaseCash' ELSE 'CylinderCash' END,abs(NEW."Cash"),NEW."Cash",'',NEW."Reason",'internal:' || NEW."Id",NEW."RequestHash",NEW."ActorId",NEW."PostedAt",NEW."BusinessDate");
              END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER sale_finance_capture AFTER INSERT ON "SaleEntries" FOR EACH ROW EXECUTE FUNCTION finance_capture_legacy();
            CREATE TRIGGER purchase_finance_capture AFTER INSERT ON "PurchaseEntries" FOR EACH ROW EXECUTE FUNCTION finance_capture_legacy();
            CREATE TRIGGER cylinder_finance_capture AFTER INSERT ON "CylinderLoanEntries" FOR EACH ROW EXECUTE FUNCTION finance_capture_legacy();
            """);
    }
    private static void RemoveGuards(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER sale_finance_capture ON "SaleEntries";
            DROP TRIGGER purchase_finance_capture ON "PurchaseEntries";
            DROP TRIGGER cylinder_finance_capture ON "CylinderLoanEntries";
            DROP FUNCTION finance_capture_legacy();
            DROP TRIGGER finance_cash_check ON "FinanceEntries";
            DROP FUNCTION finance_cash_guard();
            """);
    }
}
