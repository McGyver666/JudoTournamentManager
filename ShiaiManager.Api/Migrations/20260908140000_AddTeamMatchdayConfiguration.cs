using ShiaiManager.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiaiManager.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260908140000_AddTeamMatchdayConfiguration")]
    /// <inheritdoc />
    public partial class AddTeamMatchdayConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompetitionMode",
                table: "Tournaments",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "Individual");

            migrationBuilder.AddColumn<string>(
                name: "TeamMatchdayProfile",
                table: "Tournaments",
                type: "TEXT",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamMatchdayTeams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClubId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMatchdayTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMatchdayTeams_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamMatchdayTeams_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MatchdayWeighIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AthleteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeightKg = table.Column<decimal>(type: "TEXT", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
                name: "IX_MatchdayWeighIns_TournamentId_AthleteId",
                table: "MatchdayWeighIns",
                columns: new[] { "TournamentId", "AthleteId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchdayWeighIns_AthleteId",
                table: "MatchdayWeighIns",
                column: "AthleteId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchdayTeams_TournamentId_Name",
                table: "TeamMatchdayTeams",
                columns: new[] { "TournamentId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMatchdayTeams_ClubId",
                table: "TeamMatchdayTeams",
                column: "ClubId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MatchdayWeighIns");
            migrationBuilder.DropTable(name: "TeamMatchdayTeams");
            migrationBuilder.DropColumn(name: "CompetitionMode", table: "Tournaments");
            migrationBuilder.DropColumn(name: "TeamMatchdayProfile", table: "Tournaments");
        }
    }
}