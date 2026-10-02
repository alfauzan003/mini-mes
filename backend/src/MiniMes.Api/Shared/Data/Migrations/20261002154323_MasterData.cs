using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class MasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "carrier");

            migrationBuilder.EnsureSchema(
                name: "eqp");

            migrationBuilder.EnsureSchema(
                name: "lot");

            migrationBuilder.EnsureSchema(
                name: "wo");

            migrationBuilder.CreateTable(
                name: "carrier_type",
                schema: "carrier",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    allowed_lot_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carrier_type", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                schema: "eqp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    lane_count = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status_before_down = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    current_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "material",
                schema: "lot",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    polarity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operation",
                schema: "wo",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    output_lot_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    uom = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operation", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "product",
                schema: "wo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    polarity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "carrier",
                schema: "carrier",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    type_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    current_lot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carrier", x => x.id);
                    table.ForeignKey(
                        name: "fk_carrier_carrier_type_type_code",
                        column: x => x.type_code,
                        principalSchema: "carrier",
                        principalTable: "carrier_type",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_route",
                schema: "wo",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_route", x => new { x.product_id, x.operation });
                    table.ForeignKey(
                        name: "fk_product_route_operation_operation",
                        column: x => x.operation,
                        principalSchema: "wo",
                        principalTable: "operation",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_route_product_product_id",
                        column: x => x.product_id,
                        principalSchema: "wo",
                        principalTable: "product",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_carrier_code",
                schema: "carrier",
                table: "carrier",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_carrier_type_code_status",
                schema: "carrier",
                table: "carrier",
                columns: new[] { "type_code", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_code",
                schema: "eqp",
                table: "equipment",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_code",
                schema: "lot",
                table: "material",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_code",
                schema: "wo",
                table: "product",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_route_operation",
                schema: "wo",
                table: "product_route",
                column: "operation");

            migrationBuilder.CreateIndex(
                name: "ix_product_route_product_id_seq",
                schema: "wo",
                table: "product_route",
                columns: new[] { "product_id", "seq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "carrier",
                schema: "carrier");

            migrationBuilder.DropTable(
                name: "equipment",
                schema: "eqp");

            migrationBuilder.DropTable(
                name: "material",
                schema: "lot");

            migrationBuilder.DropTable(
                name: "product_route",
                schema: "wo");

            migrationBuilder.DropTable(
                name: "carrier_type",
                schema: "carrier");

            migrationBuilder.DropTable(
                name: "operation",
                schema: "wo");

            migrationBuilder.DropTable(
                name: "product",
                schema: "wo");
        }
    }
}
