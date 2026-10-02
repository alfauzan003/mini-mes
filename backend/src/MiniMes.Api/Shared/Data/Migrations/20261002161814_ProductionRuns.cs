using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProductionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "exec");

            migrationBuilder.CreateTable(
                name: "production_run",
                schema: "exec",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    good_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    reject_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_production_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "run_input",
                schema: "exec",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    consumed_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_input", x => x.id);
                    table.ForeignKey(
                        name: "fk_run_input_production_run_run_id",
                        column: x => x.run_id,
                        principalSchema: "exec",
                        principalTable: "production_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "run_output",
                schema: "exec",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    carrier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lane = table.Column<int>(type: "integer", nullable: true),
                    good_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    reject_qty = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_run_output", x => x.id);
                    table.ForeignKey(
                        name: "fk_run_output_production_run_run_id",
                        column: x => x.run_id,
                        principalSchema: "exec",
                        principalTable: "production_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_production_run_equipment_id",
                schema: "exec",
                table: "production_run",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_production_run_work_order_operation_id",
                schema: "exec",
                table: "production_run",
                column: "work_order_operation_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_input_lot_id",
                schema: "exec",
                table: "run_input",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_input_run_id",
                schema: "exec",
                table: "run_input",
                column: "run_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_output_lot_id",
                schema: "exec",
                table: "run_output",
                column: "lot_id");

            migrationBuilder.CreateIndex(
                name: "ix_run_output_run_id",
                schema: "exec",
                table: "run_output",
                column: "run_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "run_input",
                schema: "exec");

            migrationBuilder.DropTable(
                name: "run_output",
                schema: "exec");

            migrationBuilder.DropTable(
                name: "production_run",
                schema: "exec");
        }
    }
}
