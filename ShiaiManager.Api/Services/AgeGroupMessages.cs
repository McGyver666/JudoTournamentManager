namespace ShiaiManager.Api.Services;

/// <summary>
/// German API message paired with the frontend translation key that localizes it.
/// </summary>
/// <param name="Key">Frontend translation key.</param>
/// <param name="Text">German default text.</param>
public sealed record LocalizedMessage(string Key, string Text);

/// <summary>
/// Signals a rejected business operation whose message can be localized by the frontend.
/// </summary>
public sealed class LocalizedOperationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new exception for the given message.
    /// </summary>
    public LocalizedOperationException(LocalizedMessage message)
        : base(message.Text)
    {
        Localized = message;
    }

    /// <summary>
    /// Localizable message describing the rejection.
    /// </summary>
    public LocalizedMessage Localized { get; }
}

/// <summary>
/// API messages for age groups, start age groups, category assignment and category generation.
/// </summary>
public static class AgeGroupMessages
{
    /// <summary>Generation request without age group.</summary>
    public static readonly LocalizedMessage AgeGroupRequired = new(
        "errors.ageGroups.ageGroupRequired",
        "Die Altersklasse ist erforderlich.");

    /// <summary>Generation request without valid gender mode.</summary>
    public static readonly LocalizedMessage GenderModeRequired = new(
        "errors.ageGroups.genderModeRequired",
        "Der Geschlechtsmodus ist erforderlich.");

    /// <summary>Generation request without valid weight mode.</summary>
    public static readonly LocalizedMessage WeightModeRequired = new(
        "errors.ageGroups.weightModeRequired",
        "Die Gewichtsklassen-Strategie ist erforderlich.");

    /// <summary>Target group size out of range.</summary>
    public static readonly LocalizedMessage TargetSizeOutOfRange = new(
        "errors.ageGroups.targetSizeOutOfRange",
        "Die Zielanzahl muss zwischen 2 und 64 liegen.");

    /// <summary>Maximum weight deviation out of range.</summary>
    public static readonly LocalizedMessage MaxWeightDeviationOutOfRange = new(
        "errors.ageGroups.maxWeightDeviationOutOfRange",
        "Die maximale Gewichtsabweichung muss zwischen 0,1 und 50 kg liegen.");

    /// <summary>Mixed generation without male and female preset.</summary>
    public static readonly LocalizedMessage MixedRequiresBothGenders = new(
        "errors.ageGroups.mixedRequiresBothGenders",
        "Für Mixed müssen männliche und weibliche Presets derselben Altersklasse existieren.");

    /// <summary>No tournament preset for the selected age group and gender.</summary>
    public static readonly LocalizedMessage PresetNotFound = new(
        "errors.ageGroups.presetNotFound",
        "Für die ausgewählte Altersklasse und das Geschlecht wurde kein Turnier-Preset gefunden.");

    /// <summary>Standard classes requested for a preset without weight limits.</summary>
    public static readonly LocalizedMessage PresetWithoutStandardClasses = new(
        "errors.ageGroups.presetWithoutStandardClasses",
        "Das Preset hat keine Standardgewichtsklassen. Bitte nach Gewicht gruppieren.");

    /// <summary>Regeneration blocked by locked, drawn or fought categories.</summary>
    public static readonly LocalizedMessage CategoriesCannotBeReplaced = new(
        "errors.ageGroups.categoriesCannotBeReplaced",
        "Die Kategorien dieser Altersklasse können nicht ersetzt werden, weil mindestens eine gesperrt, ausgelost oder mit Kämpfen belegt ist.");

    /// <summary>Start age group not allowed for the athlete.</summary>
    public static readonly LocalizedMessage StartAgeGroupInvalid = new(
        "errors.ageGroups.startAgeGroupInvalid",
        "Die Startaltersklasse passt nicht zu Geschlecht und Jahrgang des Athleten.");

    /// <summary>Start age group change blocked by the current category.</summary>
    public static readonly LocalizedMessage StartAgeGroupCategoryLocked = new(
        "errors.ageGroups.startAgeGroupCategoryLocked",
        "Die Startaltersklasse kann nicht geändert werden, weil die aktuelle Kategorie gesperrt oder bereits ausgelost ist.");

    /// <summary>Category age group differs from the effective age group.</summary>
    public static readonly LocalizedMessage CategoryAgeGroupMismatch = new(
        "errors.ageGroups.categoryAgeGroupMismatch",
        "Die Kategorie muss zur effektiven Altersklasse der Meldung passen.");

    /// <summary>Category gender differs from the athlete gender.</summary>
    public static readonly LocalizedMessage CategoryGenderMismatch = new(
        "errors.ageGroups.categoryGenderMismatch",
        "Das Geschlecht der Kategorie passt nicht zur Meldung.");

    /// <summary>Athlete is heavier than the category limit.</summary>
    public static readonly LocalizedMessage WeightAboveCategoryLimit = new(
        "errors.ageGroups.weightAboveCategoryLimit",
        "Das Gewicht liegt über der Gewichtsgrenze der Kategorie.");

    /// <summary>Athlete birth year is not covered by any preset.</summary>
    public static readonly LocalizedMessage NoPresetForBirthYear = new(
        "errors.ageGroups.noPresetForBirthYear",
        "Für den Jahrgang wurde keine passende Altersklasse in den Turnier-Presets gefunden.");
}
