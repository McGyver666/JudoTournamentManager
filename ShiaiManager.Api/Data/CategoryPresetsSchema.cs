namespace ShiaiManager.Api.Data;

/// <summary>
/// SQL for the CategoryPresets table, which is created by a startup schema patch instead of a discoverable migration.
/// </summary>
internal static class CategoryPresetsSchema
{
    /// <summary>Creates the CategoryPresets table when it does not exist yet.</summary>
    public const string CreateTableSql =
        """
        CREATE TABLE IF NOT EXISTS "CategoryPresets" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_CategoryPresets" PRIMARY KEY,
            "TournamentId" TEXT NOT NULL,
            "AgeGroup" TEXT NOT NULL,
            "Gender" TEXT NOT NULL,
            "MaxAgeYears" INTEGER NULL,
            "MinAgeYears" INTEGER NULL,
            "DefaultMatchDurationSeconds" INTEGER NOT NULL DEFAULT 240,
            "WeightClassLimitsJson" TEXT NOT NULL,
            "SortOrder" INTEGER NOT NULL,
            CONSTRAINT "FK_CategoryPresets_Tournaments_TournamentId" FOREIGN KEY ("TournamentId") REFERENCES "Tournaments" ("Id") ON DELETE CASCADE
        );
        """;

    /// <summary>Creates the tournament index of the CategoryPresets table when it does not exist yet.</summary>
    public const string CreateIndexSql =
        "CREATE INDEX IF NOT EXISTS \"IX_CategoryPresets_TournamentId\" ON \"CategoryPresets\" (\"TournamentId\");";
}
