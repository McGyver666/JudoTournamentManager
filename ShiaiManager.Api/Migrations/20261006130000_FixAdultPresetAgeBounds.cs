using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ShiaiManager.Api.Data;

#nullable disable

namespace ShiaiManager.Api.Migrations;

/// <summary>
/// Corrects seeded adult presets that were stored as "at most 17 years" instead of "at least 17 years".
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20261006130000_FixAdultPresetAgeBounds")]
public partial class FixAdultPresetAgeBounds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The table normally comes from a startup schema patch, which runs after migrations on fresh databases.
        migrationBuilder.Sql(CategoryPresetsSchema.CreateTableSql);
        migrationBuilder.Sql(CategoryPresetsSchema.CreateIndexSql);

        // Only rows that still carry the faulty seed values are touched; edited presets stay as they are.
        migrationBuilder.Sql(
            """
            UPDATE "CategoryPresets"
            SET "MaxAgeYears" = NULL, "MinAgeYears" = 17
            WHERE "AgeGroup" IN ('Männer', 'Frauen')
              AND "MaxAgeYears" = 17
              AND "MinAgeYears" IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "CategoryPresets"
            SET "MaxAgeYears" = 17, "MinAgeYears" = NULL
            WHERE "AgeGroup" IN ('Männer', 'Frauen')
              AND "MaxAgeYears" IS NULL
              AND "MinAgeYears" = 17;
            """);
    }
}
