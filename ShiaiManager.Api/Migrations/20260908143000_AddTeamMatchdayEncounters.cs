using ShiaiManager.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShiaiManager.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260908143000_AddTeamMatchdayEncounters")]
    /// <inheritdoc />
    public partial class AddTeamMatchdayEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TeamMatchdayWeightClassOrderJson",
                table: "Tournaments",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamEncounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HomeTeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AwayTeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TatamiId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    NoShowTeamId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamEncounters", x => x.Id);
                    table.ForeignKey("FK_TeamEncounters_TeamMatchdayTeams_AwayTeamId", x => x.AwayTeamId, "TeamMatchdayTeams", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_TeamEncounters_TeamMatchdayTeams_HomeTeamId", x => x.HomeTeamId, "TeamMatchdayTeams", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_TeamEncounters_Tatamis_TatamiId", x => x.TatamiId, "Tatamis", "Id", onDelete: ReferentialAction.SetNull);
                    table.ForeignKey("FK_TeamEncounters_Tournaments_TournamentId", x => x.TournamentId, "Tournaments", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamLineupEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EncounterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LegNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    WeightClassIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    AthleteId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineupEntries", x => x.Id);
                    table.ForeignKey("FK_TeamLineupEntries_Athletes_AthleteId", x => x.AthleteId, "Athletes", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_TeamLineupEntries_TeamEncounters_EncounterId", x => x.EncounterId, "TeamEncounters", "Id", onDelete: ReferentialAction.Cascade);
                    table.ForeignKey("FK_TeamLineupEntries_TeamMatchdayTeams_TeamId", x => x.TeamId, "TeamMatchdayTeams", "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterBouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EncounterId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LegNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightClassIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    FightId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterBouts", x => x.Id);
                    table.ForeignKey("FK_EncounterBouts_Fights_FightId", x => x.FightId, "Fights", "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey("FK_EncounterBouts_TeamEncounters_EncounterId", x => x.EncounterId, "TeamEncounters", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(name: "IX_TeamEncounters_AwayTeamId", table: "TeamEncounters", column: "AwayTeamId");
            migrationBuilder.CreateIndex(name: "IX_TeamEncounters_HomeTeamId", table: "TeamEncounters", column: "HomeTeamId");
            migrationBuilder.CreateIndex(name: "IX_TeamEncounters_TatamiId", table: "TeamEncounters", column: "TatamiId");
            migrationBuilder.CreateIndex(name: "IX_TeamEncounters_TournamentId_DisplayOrder", table: "TeamEncounters", columns: new[] { "TournamentId", "DisplayOrder" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_TeamLineupEntries_AthleteId", table: "TeamLineupEntries", column: "AthleteId");
            migrationBuilder.CreateIndex(name: "IX_TeamLineupEntries_TeamId", table: "TeamLineupEntries", column: "TeamId");
            migrationBuilder.CreateIndex(name: "IX_TeamLineupEntries_EncounterId_LegNumber_TeamId_WeightClassIndex", table: "TeamLineupEntries", columns: new[] { "EncounterId", "LegNumber", "TeamId", "WeightClassIndex" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_EncounterBouts_FightId", table: "EncounterBouts", column: "FightId", unique: true);
            migrationBuilder.CreateIndex(name: "IX_EncounterBouts_EncounterId_LegNumber_WeightClassIndex", table: "EncounterBouts", columns: new[] { "EncounterId", "LegNumber", "WeightClassIndex" }, unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "EncounterBouts");
            migrationBuilder.DropTable(name: "TeamLineupEntries");
            migrationBuilder.DropTable(name: "TeamEncounters");
            migrationBuilder.DropColumn(name: "TeamMatchdayWeightClassOrderJson", table: "Tournaments");
        }
    }
}