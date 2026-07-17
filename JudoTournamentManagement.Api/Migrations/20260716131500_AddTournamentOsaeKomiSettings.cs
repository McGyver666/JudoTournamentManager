using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using JudoTournamentManagement.Api.Data;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
        [DbContext(typeof(AppDbContext))]
    [Migration("20260716131500_AddTournamentOsaeKomiSettings")]
    public partial class AddTournamentOsaeKomiSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OsaeKomiIpponSeconds",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "OsaeKomiWazaAriSeconds",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<bool>(
                name: "OsaeKomiYukoEnabled",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "OsaeKomiYukoSeconds",
                table: "Tournaments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 5);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OsaeKomiIpponSeconds",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "OsaeKomiWazaAriSeconds",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "OsaeKomiYukoEnabled",
                table: "Tournaments");

            migrationBuilder.DropColumn(
                name: "OsaeKomiYukoSeconds",
                table: "Tournaments");
        }
    }
}