using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAthleteLastFightMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastFightDurationSeconds",
                table: "Athletes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastFightEndedAtUtc",
                table: "Athletes",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFightDurationSeconds",
                table: "Athletes");

            migrationBuilder.DropColumn(
                name: "LastFightEndedAtUtc",
                table: "Athletes");
        }
    }
}
