using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTournamentMinimumRestBetweenFights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumRestBetweenFightsSeconds",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 180);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumRestBetweenFightsSeconds",
                table: "Tournaments");
        }
    }
}
