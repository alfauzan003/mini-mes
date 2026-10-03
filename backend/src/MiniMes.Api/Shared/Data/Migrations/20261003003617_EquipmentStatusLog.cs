using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentStatusLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "equipment_status_log",
                schema: "eqp",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    to = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_status_log", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_equipment_status_log_equipment_id_changed_at",
                schema: "eqp",
                table: "equipment_status_log",
                columns: new[] { "equipment_id", "changed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "equipment_status_log",
                schema: "eqp");
        }
    }
}
