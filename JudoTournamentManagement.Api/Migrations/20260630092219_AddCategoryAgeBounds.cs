using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryAgeBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    User = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tournaments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Venue = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Organizer = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tournaments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AgeGroup = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    WeightClassKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    MinBirthYear = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxBirthYear = table.Column<int>(type: "INTEGER", nullable: true),
                    RulesetNotes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    MatchDurationSeconds = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 300),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Clubs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clubs_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Tatamis",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tatamis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tatamis_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Athletes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ClubId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    BirthYear = table.Column<int>(type: "INTEGER", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LicenseId = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    WeightKg = table.Column<decimal>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Athletes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Athletes_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Athletes_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Fights",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    BracketType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Round = table.Column<int>(type: "INTEGER", nullable: false),
                    FightNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    WhiteAthleteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BlueAthleteId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WinnerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    IsBye = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TatamiId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WhiteScore = table.Column<int>(type: "INTEGER", nullable: false),
                    BlueScore = table.Column<int>(type: "INTEGER", nullable: false),
                    WhitePenalties = table.Column<int>(type: "INTEGER", nullable: false),
                    BluePenalties = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fights_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fights_Tatamis_TatamiId",
                        column: x => x.TatamiId,
                        principalTable: "Tatamis",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Fights_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Registrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TournamentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AthleteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Registrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Registrations_Athletes_AthleteId",
                        column: x => x.AthleteId,
                        principalTable: "Athletes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Registrations_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Registrations_Tournaments_TournamentId",
                        column: x => x.TournamentId,
                        principalTable: "Tournaments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Athletes_ClubId",
                table: "Athletes",
                column: "ClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Athletes_TournamentId",
                table: "Athletes",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TournamentId",
                table: "AuditLogs",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_TournamentId",
                table: "Categories",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_TournamentId",
                table: "Clubs",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Fights_CategoryId",
                table: "Fights",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Fights_TatamiId",
                table: "Fights",
                column: "TatamiId");

            migrationBuilder.CreateIndex(
                name: "IX_Fights_TournamentId",
                table: "Fights",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_AthleteId_TournamentId",
                table: "Registrations",
                columns: new[] { "AthleteId", "TournamentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_CategoryId",
                table: "Registrations",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Registrations_TournamentId",
                table: "Registrations",
                column: "TournamentId");

            migrationBuilder.CreateIndex(
                name: "IX_Tatamis_TournamentId",
                table: "Tatamis",
                column: "TournamentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "Fights");

            migrationBuilder.DropTable(
                name: "Registrations");

            migrationBuilder.DropTable(
                name: "Tatamis");

            migrationBuilder.DropTable(
                name: "Athletes");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "Clubs");

            migrationBuilder.DropTable(
                name: "Tournaments");
        }
    }
}
