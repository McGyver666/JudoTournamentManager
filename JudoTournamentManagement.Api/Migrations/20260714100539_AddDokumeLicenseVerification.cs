using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JudoTournamentManagement.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDokumeLicenseVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LicenseCheckPassed",
                table: "Registrations",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseNumber",
                table: "Registrations",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseOverrideReason",
                table: "Registrations",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LicenseVerifiedAtUtc",
                table: "Registrations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LicenseVerifiedByUser",
                table: "Registrations",
                type: "TEXT",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PassExpiryDate",
                table: "Registrations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LicenseCheckPassed",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "LicenseNumber",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "LicenseOverrideReason",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "LicenseVerifiedAtUtc",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "LicenseVerifiedByUser",
                table: "Registrations");

            migrationBuilder.DropColumn(
                name: "PassExpiryDate",
                table: "Registrations");
        }
    }
}
