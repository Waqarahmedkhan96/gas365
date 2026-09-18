using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Gas365.Infrastructure.Migrations;

public partial class InventoryGuards : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT DISTINCT unnest("Permissions" || ARRAY['inventory.view','catalog.manage','inventory.post','inventory.reverse'])) WHERE "IsOwner" = TRUE;
            CREATE FUNCTION inventory_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Posted inventory records are immutable' USING ERRCODE = '23514'; END;
            $$;
            CREATE TRIGGER stock_documents_immutable BEFORE UPDATE OR DELETE ON "StockDocuments" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            CREATE TRIGGER stock_movements_immutable BEFORE UPDATE OR DELETE ON "StockMovements" FOR EACH ROW EXECUTE FUNCTION inventory_append_only();
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP FUNCTION inventory_append_only() CASCADE;
            UPDATE "TenantRoles" SET "Permissions" = ARRAY(SELECT p FROM unnest("Permissions") AS p WHERE p NOT IN ('inventory.view','catalog.manage','inventory.post','inventory.reverse'));
            """);
    }
}
