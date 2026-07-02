using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelWithCurrentSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LicenseConfirmed",
                table: "Registrations",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "BlueIpponCount",
                table: "Fights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BlueWazaAriCount",
                table: "Fights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BlueYukoCount",
                table: "Fights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OsaeKomiSide",
                table: "Fights",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OsaeKomiStartedAtUtc",
                table: "Fights",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAtUtc",
                table: "Fights",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PoolNumber",
                table: "Fights",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WhiteIpponCount",
                table: "Fights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WhiteWazaAriCount",
                table: "Fights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WhiteYukoCount",
                table: "Fights",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DrawFormat",
                table: "Categories",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GoldenScoreDurationSeconds",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: 180);

            migrationBuilder.AddColumn<bool>(
                name: "GoldenScoreEnabled",
                table: "Categories",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    PasswordHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PasswordSalt = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PasswordIterations = table.Column<int>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailedLoginCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LockedUntilUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserAccountId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "BLOB", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuthSessions_UserAccounts_UserAccountId",
                        column: x => x.UserAccountId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_TokenHash",
                table: "AuthSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuthSessions_UserAccountId_ExpiresAtUtc",
                table: "AuthSessions",
                columns: new[] { "UserAccountId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_NormalizedUserName",
                table: "UserAccounts",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthSessions");

            migrationBuilder.DropTable(
                name: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "LicenseConfirmed",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "BlueIpponCount",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "BlueWazaAriCount",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "BlueYukoCount",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "OsaeKomiSide",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "OsaeKomiStartedAtUtc",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "PausedAtUtc",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "PoolNumber",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "WhiteIpponCount",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "WhiteWazaAriCount",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "WhiteYukoCount",
                table: "Fights");

            migrationBuilder.DropColumn(
                name: "DrawFormat",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "GoldenScoreDurationSeconds",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "GoldenScoreEnabled",
                table: "Categories");
        }
    }
}
