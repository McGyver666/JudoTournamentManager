using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiaiManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddOsaeKomiPause : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "OsaeKomiElapsedMilliseconds",
                table: "Fights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OsaeKomiPausedAtUtc",
                table: "Fights",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsaeKomiElapsedMilliseconds",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "OsaeKomiPausedAtUtc",
                table: "Fights");
        }
    }
}
