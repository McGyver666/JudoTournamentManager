using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiaiManager.Api.Data;

#nullable disable

namespace ShiaiManager.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261006120000_AddRegistrationStartAgeGroup")]
public partial class AddRegistrationStartAgeGroup : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "StartAgeGroup",
            table: "Registrations",
            type: "TEXT",
            maxLength: 40,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StartAgeGroup",
            table: "Registrations");
    }
}