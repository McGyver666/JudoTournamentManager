using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiaiManager.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTeamMatchdayWeighIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MatchdayWeighIns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MatchdayWeighIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AthleteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    WeightKg = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MatchdayWeighIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MatchdayWeighIns_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MatchdayWeighIns_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MatchdayWeighIns_AthleteId",
                table: "MatchdayWeighIns",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_MatchdayWeighIns_TournamentId_AthleteId",
                table: "MatchdayWeighIns",
                columns: new[] { "TournamentId", "AthleteId" },
                unique: true);
        }
    }
}
