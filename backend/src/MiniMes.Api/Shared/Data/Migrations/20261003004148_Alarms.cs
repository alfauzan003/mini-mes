using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class Alarms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "alarm");

            migrationBuilder.CreateTable(
                name: "alarm_code",
                schema: "alarm",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    operation = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alarm_code", x => x.code);
                    table.ForeignKey(
                        name: "fk_alarm_code_operation_operation",
                        column: x => x.operation,
                        principalSchema: "wo",
                        principalTable: "operation",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alarm",
                schema: "alarm",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alarm_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cleared_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    acknowledged_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alarm", x => x.id);
                    table.ForeignKey(
                        name: "fk_alarm_alarm_code_alarm_code",
                        column: x => x.alarm_code,
                        principalSchema: "alarm",
                        principalTable: "alarm_code",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alarm_equipment_equipment_id",
                        column: x => x.equipment_id,
                        principalSchema: "eqp",
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_alarm_user_acknowledged_by_id",
                        column: x => x.acknowledged_by_id,
                        principalSchema: "identity",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alarm_acknowledged_by_id",
                schema: "alarm",
                table: "alarm",
                column: "acknowledged_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_alarm_alarm_code",
                schema: "alarm",
                table: "alarm",
                column: "alarm_code");

            migrationBuilder.CreateIndex(
                name: "ix_alarm_equipment_id_alarm_code",
                schema: "alarm",
                table: "alarm",
                columns: new[] { "equipment_id", "alarm_code" },
                unique: true,
                filter: "cleared_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_alarm_equipment_id_raised_at",
                schema: "alarm",
                table: "alarm",
                columns: new[] { "equipment_id", "raised_at" });

            migrationBuilder.CreateIndex(
                name: "ix_alarm_code_operation",
                schema: "alarm",
                table: "alarm_code",
                column: "operation");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alarm",
                schema: "alarm");

            migrationBuilder.DropTable(
                name: "alarm_code",
                schema: "alarm");
        }
    }
}
