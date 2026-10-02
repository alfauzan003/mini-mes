using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniMes.Api.Shared.Data.Migrations
{
    /// <inheritdoc />
    public partial class IdSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "id_sequence",
                schema: "lot",
                columns: table => new
                {
                    prefix = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_id_sequence", x => x.prefix);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "id_sequence",
                schema: "lot");
        }
    }
}
