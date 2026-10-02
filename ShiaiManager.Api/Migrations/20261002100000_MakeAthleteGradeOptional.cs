using Microsoft.EntityFrameworkCore.Migrations;
using ShiaiManager.Api.Data;

#nullable disable

namespace ShiaiManager.Api.Migrations;

[Migration("20261002100000_MakeAthleteGradeOptional")]
public partial class MakeAthleteGradeOptional : Migration
{
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

        migrationBuilder.AlterColumn<int>(
            name: "Grade",
            table: "Athletes",
            type: "INTEGER",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "INTEGER",
            oldDefaultValue: 1);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE Athletes SET Grade = 1 WHERE Grade IS NULL;");

        migrationBuilder.AlterColumn<int>(
            name: "Grade",
            table: "Athletes",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1,
            oldClrType: typeof(int),
            oldType: "INTEGER",
            oldNullable: true);
    }
}