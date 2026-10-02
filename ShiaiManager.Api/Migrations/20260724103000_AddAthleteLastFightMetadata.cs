using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiaiManager.Api.Data;

#nullable disable

namespace ShiaiManager.Api.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20260724103000_AddAthleteLastFightMetadata")]
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
