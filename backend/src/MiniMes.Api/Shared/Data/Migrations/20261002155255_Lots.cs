using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class Lots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "genealogy",
                schema: "lot",
                columns: table => new
                {
                    parent_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_genealogy", x => new { x.parent_lot_id, x.child_lot_id });
                });

            migrationBuilder.CreateTable(
                name: "lot",
                schema: "lot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    polarity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    material_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    qty = table.Column<decimal>(type: "numeric(12,3)", nullable: false),
                    uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    quality = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    current_operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    next_operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    current_equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_carrier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lot_event",
                schema: "lot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    carrier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qty = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lot_event", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_genealogy_child_lot_id",
                schema: "lot",
                table: "genealogy",
                column: "child_lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_genealogy_run_id",
                schema: "lot",
                table: "genealogy",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_lot_lot_id",
                schema: "lot",
                table: "lot",
                column: "lot_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lot_status_type",
                schema: "lot",
                table: "lot",
                columns: new[] { "status", "type" });

            migrationBuilder.CreateIndex(
                name: "ix_lot_work_order_id",
                schema: "lot",
                table: "lot",
                column: "work_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_lot_event_lot_id_occurred_at",
                schema: "lot",
                table: "lot_event",
                columns: new[] { "lot_id", "occurred_at" });

            migrationBuilder.Sql("""
                CREATE FUNCTION lot.lot_event_append_only() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'lot_event is append-only';
                END;
                $$ LANGUAGE plpgsql;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER lot_event_append_only
                BEFORE UPDATE OR DELETE ON lot.lot_event
                FOR EACH ROW EXECUTE FUNCTION lot.lot_event_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER lot_event_append_only ON lot.lot_event;");
            migrationBuilder.Sql("DROP FUNCTION lot.lot_event_append_only();");

            migrationBuilder.DropTable(
                name: "genealogy",
                schema: "lot");

            migrationBuilder.DropTable(
                name: "lot",
                schema: "lot");

            migrationBuilder.DropTable(
                name: "lot_event",
                schema: "lot");
        }
    }
}
