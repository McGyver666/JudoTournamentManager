using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFightSlotSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BlueSourceFightId",
                table: "Fights",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlueSourceOutcome",
                table: "Fights",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "WhiteSourceFightId",
                table: "Fights",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhiteSourceOutcome",
                table: "Fights",
                type: "TEXT",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BlueSourceFightId",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "BlueSourceOutcome",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "WhiteSourceFightId",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "WhiteSourceOutcome",
                table: "Fights");
        }
    }
}
