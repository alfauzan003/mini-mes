using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class Quality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "qc");

            migrationBuilder.CreateTable(
                name: "defect_code",
                schema: "qc",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_defect_code", x => x.code);
                    table.ForeignKey(
                        name: "fk_defect_code_operation_operation",
                        column: x => x.operation,
                        principalSchema: "wo",
                        principalTable: "operation",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inspection_spec",
                schema: "qc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    item_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    lsl = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    usl = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inspection_spec", x => x.id);
                    table.ForeignKey(
                        name: "fk_inspection_spec_operation_operation",
                        column: x => x.operation,
                        principalSchema: "wo",
                        principalTable: "operation",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inspection_spec_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "wo",
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inspection",
                schema: "qc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    inspector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    result = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    defect_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    reject_qty = table.Column<decimal>(type: "numeric(12,3)", nullable: true),
                    disposition = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    disposition_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    disposition_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    disposition_reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inspection", x => x.id);
                    table.ForeignKey(
                        name: "fk_inspection_defect_code_defect_code",
                        column: x => x.defect_code,
                        principalSchema: "qc",
                        principalTable: "defect_code",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inspection_lot_lot_id",
                        column: x => x.lot_id,
                        principalSchema: "lot",
                        principalTable: "lot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inspection_operation_operation",
                        column: x => x.operation,
                        principalSchema: "wo",
                        principalTable: "operation",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "inspection_measurement",
                schema: "qc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inspection_id = table.Column<Guid>(type: "uuid", nullable: false),
                    spec_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    lsl = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    usl = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    value = table.Column<decimal>(type: "numeric(12,4)", nullable: false),
                    judgment = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inspection_measurement", x => x.id);
                    table.ForeignKey(
                        name: "fk_inspection_measurement_inspection_inspection_id",
                        column: x => x.inspection_id,
                        principalSchema: "qc",
                        principalTable: "inspection",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inspection_measurement_inspection_spec_spec_id",
                        column: x => x.spec_id,
                        principalSchema: "qc",
                        principalTable: "inspection_spec",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_defect_code_operation",
                schema: "qc",
                table: "defect_code",
                column: "operation");

            migrationBuilder.CreateIndex(
                name: "ix_inspection_defect_code",
                schema: "qc",
                table: "inspection",
                column: "defect_code");

            migrationBuilder.CreateIndex(
                name: "ix_inspection_lot_id_inspected_at",
                schema: "qc",
                table: "inspection",
                columns: new[] { "lot_id", "inspected_at" });

            migrationBuilder.CreateIndex(
                name: "ix_inspection_operation",
                schema: "qc",
                table: "inspection",
                column: "operation");

            migrationBuilder.CreateIndex(
                name: "ix_inspection_measurement_inspection_id",
                schema: "qc",
                table: "inspection_measurement",
                column: "inspection_id");

            migrationBuilder.CreateIndex(
                name: "ix_inspection_measurement_spec_id",
                schema: "qc",
                table: "inspection_measurement",
                column: "spec_id");

            migrationBuilder.CreateIndex(
                name: "ix_inspection_spec_operation",
                schema: "qc",
                table: "inspection_spec",
                column: "operation");

            migrationBuilder.CreateIndex(
                name: "ix_inspection_spec_product_id_operation_item_name",
                schema: "qc",
                table: "inspection_spec",
                columns: new[] { "product_id", "operation", "item_name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inspection_measurement",
                schema: "qc");

            migrationBuilder.DropTable(
                name: "inspection",
                schema: "qc");

            migrationBuilder.DropTable(
                name: "inspection_spec",
                schema: "qc");

            migrationBuilder.DropTable(
                name: "defect_code",
                schema: "qc");
        }
    }
}
