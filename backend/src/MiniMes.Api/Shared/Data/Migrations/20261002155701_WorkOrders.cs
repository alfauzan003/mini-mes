using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class WorkOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "work_order",
                schema: "wo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_qty = table.Column<int>(type: "integer", nullable: false),
                    planned_start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    planned_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status_before_hold = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    good_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_order_operation",
                schema: "wo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_order_operation", x => x.id);
                    table.ForeignKey(
                        name: "fk_work_order_operation_operation_operation",
                        column: x => x.operation,
                        principalSchema: "wo",
                        principalTable: "operation",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_order_operation_work_order_work_order_id",
                        column: x => x.work_order_id,
                        principalSchema: "wo",
                        principalTable: "work_order",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_order_number",
                schema: "wo",
                table: "work_order",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_work_order_product_id",
                schema: "wo",
                table: "work_order",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_status",
                schema: "wo",
                table: "work_order",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_operation_equipment_id",
                schema: "wo",
                table: "work_order_operation",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_operation_operation",
                schema: "wo",
                table: "work_order_operation",
                column: "operation");

            migrationBuilder.CreateIndex(
                name: "ix_work_order_operation_work_order_id_operation",
                schema: "wo",
                table: "work_order_operation",
                columns: new[] { "work_order_id", "operation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_order_operation",
                schema: "wo");

            migrationBuilder.DropTable(
                name: "work_order",
                schema: "wo");
        }
    }
}
