using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentTwoThirdPlacesInRoundRobin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "TwoThirdPlacesInRoundRobin",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TwoThirdPlacesInRoundRobin",
                table: "Tournaments");
        }
    }
}
