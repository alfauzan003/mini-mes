using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class Parameters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "parameter_definition",
                schema: "eqp",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    setpoint = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    low = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    high = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    low_alarm_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    high_alarm_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parameter_definition", x => x.id);
                    table.ForeignKey(
                        name: "fk_parameter_definition_operation_operation",
                        column: x => x.operation,
                        principalSchema: "wo",
                        principalTable: "operation",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "parameter_reading",
                schema: "eqp",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_parameter_reading", x => x.id);
                    table.ForeignKey(
                        name: "fk_parameter_reading_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalSchema: "eqp",
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definition_operation_name",
                schema: "eqp",
                table: "parameter_definition",
                columns: new[] { "operation", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parameter_reading_equipment_id_parameter_recorded_at",
                schema: "eqp",
                table: "parameter_reading",
                columns: new[] { "equipment_id", "parameter", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_parameter_reading_recorded_at",
                schema: "eqp",
                table: "parameter_reading",
                column: "recorded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "parameter_definition",
                schema: "eqp");

            migrationBuilder.DropTable(
                name: "parameter_reading",
                schema: "eqp");
        }
    }
}
