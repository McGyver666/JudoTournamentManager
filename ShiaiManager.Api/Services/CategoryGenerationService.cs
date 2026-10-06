using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using Microsoft.Extensions.Logging;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Default category generation implementation for the assistant workflow.
/// </summary>
public sealed class CategoryGenerationService : ICategoryGenerationService
{
    private const string GeneratedMarker = "[AUTO_GENERATED]";

    private readonly ICategoriesStore _categoriesStore;
    private readonly IRegistrationsStore _registrationsStore;
    private readonly ICategoryPresetsStore _categoryPresetsStore;
    private readonly ILogger<CategoryGenerationService> _logger;

    public CategoryGenerationService(
        ICategoriesStore categoriesStore,
        IRegistrationsStore registrationsStore,
        ICategoryPresetsStore categoryPresetsStore,
        ILogger<CategoryGenerationService> logger)
    {
        ArgumentNullException.ThrowIfNull(categoriesStore);
        ArgumentNullException.ThrowIfNull(registrationsStore);
        ArgumentNullException.ThrowIfNull(categoryPresetsStore);
        ArgumentNullException.ThrowIfNull(logger);

        _categoriesStore = categoriesStore;
        _registrationsStore = registrationsStore;
        _categoryPresetsStore = categoryPresetsStore;
        _logger = logger;
    }

    public async Task<CategoryGenerationPreviewResponse> PreviewAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        var proposals = await BuildProposalsAsync(tournamentId, request, cancellationToken);
        return new CategoryGenerationPreviewResponse(
            proposals.Categories.Count,
            proposals.Categories,
            proposals.Warnings)
        {
            CategoriesToReplace = proposals.CategoriesToReplace,
            AffectedAthletes = proposals.AffectedAthletes,
            CanApply = proposals.CanApply
        };
    }

    public async Task<CategoryGenerationApplyResponse> ApplyAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        var proposals = await BuildProposalsAsync(tournamentId, request, cancellationToken);
        var warnings = proposals.Warnings.ToList();

        if (!proposals.CanApply)
        {
            throw new InvalidOperationException(
                "Die Kategorien dieser Altersklasse können nicht ersetzt werden, weil mindestens eine gesperrt oder bereits ausgelost ist.");
        }

        var deletedCount = 0;
        foreach (var category in proposals.CategoriesToReplace)
        {
            if (await _categoriesStore.DeleteAsync(category.Id, cancellationToken))
            {
                deletedCount++;
            }
        }

        const int skippedLockedCount = 0;
        var skippedDuplicateCount = 0;
        var created = new List<Category>();

        foreach (var proposal in proposals.Categories)
        {
            var createdCategory = await _categoriesStore.CreateAsync(
                tournamentId,
                proposal.Name,
                proposal.AgeGroup,
                proposal.Gender,
                proposal.WeightClassKg,
                proposal.MinBirthYear,
                proposal.MaxBirthYear,
                BuildGeneratedNote(proposal.Source),
                proposal.MatchDurationSeconds,
                proposal.GoldenScoreEnabled,
                proposal.GoldenScoreDurationSeconds,
                cancellationToken);

            if (createdCategory is null)
            {
                skippedDuplicateCount++;
                continue;
            }

            created.Add(createdCategory);
        }

        if (skippedDuplicateCount > 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningDuplicatesSkipped", skippedDuplicateCount));
        }

        _logger.LogInformation(
            "Category generation applied for tournament {TournamentId}: created={Created}, deleted={Deleted}, duplicateSkipped={DuplicateSkipped}, lockedSkipped={LockedSkipped}.",
            tournamentId,
            created.Count,
            deletedCount,
            skippedDuplicateCount,
            skippedLockedCount);

        return new CategoryGenerationApplyResponse(
            created.Count,
            deletedCount,
            skippedDuplicateCount,
            skippedLockedCount,
            created,
            warnings);
    }

    private async Task<ProposalBuildResult> BuildProposalsAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var warnings = new List<CategoryGenerationWarning>();
        var registrations = await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken);

        var storedPresets = await _categoryPresetsStore.GetAllAsync(tournamentId, cancellationToken);
        var ageGroupPresets = storedPresets
            .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.AgeGroup, request.AgeGroup.Trim()))
            .ToList();
        var matchingPresets = ageGroupPresets
            .Where(p => request.GenderMode switch
            {
                CategoryGenerationGenderMode.Male => p.Gender == Gender.Male,
                CategoryGenerationGenderMode.Female => p.Gender == Gender.Female,
                CategoryGenerationGenderMode.Mixed => p.Gender is Gender.Male or Gender.Female,
                _ => false
            })
            .ToList();

        if (request.GenderMode == CategoryGenerationGenderMode.Mixed
            && (!matchingPresets.Any(p => p.Gender == Gender.Male)
                || !matchingPresets.Any(p => p.Gender == Gender.Female)))
        {
            throw new InvalidOperationException("Für Mixed müssen männliche und weibliche Presets derselben Altersklasse existieren.");
        }

        if (matchingPresets.Count == 0)
        {
            throw new InvalidOperationException("Für die ausgewählte Altersklasse und das Geschlecht wurde kein Turnier-Preset gefunden.");
        }

        if (request.WeightMode == CategoryGenerationWeightMode.StandardClasses
            && matchingPresets.Any(p => p.WeightClassLimitsKg.Count == 0))
        {
            throw new InvalidOperationException("Das Preset hat keine Standardgewichtsklassen. Bitte nach Gewicht gruppieren.");
        }

        var eligibleRegistrations = registrations
            .Where(r => request.GenderMode switch
            {
                CategoryGenerationGenderMode.Male => r.AthleteGender == Gender.Male,
                CategoryGenerationGenderMode.Female => r.AthleteGender == Gender.Female,
                CategoryGenerationGenderMode.Mixed => r.AthleteGender is Gender.Male or Gender.Female,
                _ => false
            })
            .Where(r => StringComparer.OrdinalIgnoreCase.Equals(
                AgeGroupResolver.GetEffectiveAgeGroup(r.AthleteBirthYear, r.AthleteGender, r.StartAgeGroup, storedPresets),
                request.AgeGroup.Trim()))
            .ToList();

        var registrationsWithoutPreset = registrations.Count(r =>
            AgeGroupResolver.GetEffectiveAgeGroup(r.AthleteBirthYear, r.AthleteGender, r.StartAgeGroup, storedPresets) is null);
        if (registrationsWithoutPreset > 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningRegistrationsWithoutPreset", registrationsWithoutPreset));
        }

        if (request.WeightMode == CategoryGenerationWeightMode.StandardClasses)
        {
            var registrationsWithoutWeight = eligibleRegistrations.Count(registration => !registration.AthleteWeightKg.HasValue);
            if (registrationsWithoutWeight > 0)
            {
                warnings.Add(new CategoryGenerationWarning("categories.warningStandardClassesWithoutWeight", registrationsWithoutWeight));
            }
        }

        var categories = request.WeightMode switch
        {
            CategoryGenerationWeightMode.StandardClasses => BuildStandardProposals(matchingPresets, request, eligibleRegistrations, warnings),
            CategoryGenerationWeightMode.AthletesByTargetSize => BuildAthleteDrivenProposals(request, eligibleRegistrations, warnings, matchingPresets),
            _ => throw new InvalidOperationException("Unbekannte Gewichtsklassen-Strategie.")
        };

        var uniqueCategories = categories
            .GroupBy(c => new { c.AgeGroup, c.Gender, c.WeightClassKg })
            .Select(g => g.First())
            .OrderBy(c => c.AgeGroup)
            .ThenBy(c => c.Gender)
            .ThenBy(c => c.WeightClassKg ?? decimal.MaxValue)
            .ToList();

        var categoriesToReplace = (await _categoriesStore.GetAllAsync(tournamentId, cancellationToken) ?? [])
            .Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.AgeGroup, request.AgeGroup.Trim()))
            .Where(c => IsSelectedGender(c.Gender, request.GenderMode!.Value))
            .ToList();
        var canApply = !categoriesToReplace.Any(c => c.IsLocked || c.DrawFormat.HasValue);
        if (!canApply)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningCategoriesLocked"));
        }

        var affectedAthletes = eligibleRegistrations
            .Select(registration => new GeneratedAthletePreview(
                registration.Id,
                registration.AthleteFirstName,
                registration.AthleteLastName,
                registration.AthleteBirthYear,
                registration.AthleteGender,
                registration.AthleteWeightKg,
                registration.StartAgeGroup))
            .ToList();

        return new ProposalBuildResult(uniqueCategories, warnings, categoriesToReplace, affectedAthletes, canApply);
    }

    private static void ValidateRequest(GenerateCategoriesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AgeGroup))
        {
            throw new InvalidOperationException("Die Altersklasse ist erforderlich.");
        }

        if (request.GenderMode is null)
        {
            throw new InvalidOperationException("Der Geschlechtsmodus ist erforderlich.");
        }

        if (request.WeightMode is null)
        {
            throw new InvalidOperationException("Die Gewichtsklassen-Strategie ist erforderlich.");
        }

        if (request.TargetAthletesPerCategory is < 2 or > 64)
        {
            throw new InvalidOperationException("Die Zielanzahl muss zwischen 2 und 64 liegen.");
        }

        if (request.MaxWeightDeviationKg is < 0.1m or > 50m)
        {
            throw new InvalidOperationException("Die maximale Gewichtsabweichung muss zwischen 0,1 und 50 kg liegen.");
        }
    }

    private static bool IsSelectedGender(Gender categoryGender, CategoryGenerationGenderMode mode) =>
        mode switch
        {
            CategoryGenerationGenderMode.Male => categoryGender == Gender.Male,
            CategoryGenerationGenderMode.Female => categoryGender == Gender.Female,
            CategoryGenerationGenderMode.Mixed => categoryGender == Gender.Mixed,
            _ => false
        };

    private static List<GeneratedCategoryProposal> BuildStandardProposals(
        IReadOnlyList<TournamentCategoryPreset> storedPresets,
        GenerateCategoriesRequest request,
        IReadOnlyList<RegistrationDetail> registrations,
        List<CategoryGenerationWarning> warnings)
    {
        var mode = request.GenderMode!.Value;
        var rows = storedPresets
            .Where(p => mode switch
            {
                CategoryGenerationGenderMode.Male => p.Gender == Gender.Male,
                CategoryGenerationGenderMode.Female => p.Gender == Gender.Female,
                CategoryGenerationGenderMode.Mixed => true,
                _ => false
            })
            .Where(p => string.IsNullOrWhiteSpace(request.AgeGroup)
                || StringComparer.OrdinalIgnoreCase.Equals(p.AgeGroup, request.AgeGroup.Trim()))
            .Select(p => new StandardPresetRow(
                p.AgeGroup,
                mode == CategoryGenerationGenderMode.Mixed ? Gender.Mixed : p.Gender,
                p.MinBirthYear,
                p.MaxBirthYear,
                p.DefaultMatchDurationSeconds,
                p.WeightClassLimitsKg))
            .ToList();

        // For Mixed mode merge male and female rows by normalized age group
        if (mode == CategoryGenerationGenderMode.Mixed)
        {
            rows = MergeToBeMixedRows(rows);
        }

        if (rows.Count == 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningNoStandardClasses"));
            return [];
        }

        var proposals = new List<GeneratedCategoryProposal>();
        foreach (var row in rows)
        {
            decimal? previousLimit = null;
            foreach (var limit in row.WeightClassLimitsKg)
            {
                var minYear = row.MinBirthYear;
                var maxYear = row.MaxBirthYear;
                var gender = mode == CategoryGenerationGenderMode.Mixed ? Gender.Mixed : row.Gender;
                var estimatedAthletes = CountMatchingAthletes(
                    registrations,
                    gender,
                    minYear,
                    maxYear,
                    previousLimit,
                    limit);

                proposals.Add(new GeneratedCategoryProposal(
                    BuildCategoryName(row.AgeGroup, gender, limit, previousLimit),
                    row.AgeGroup,
                    gender,
                    limit,
                    minYear,
                    maxYear,
                    request.MatchDurationSeconds,
                    request.GoldenScoreEnabled,
                    request.GoldenScoreDurationSeconds,
                    estimatedAthletes,
                    "standard"));

                if (limit.HasValue)
                {
                    previousLimit = limit;
                }
            }
        }

        return proposals;
    }

    private static List<GeneratedCategoryProposal> BuildAthleteDrivenProposals(
        GenerateCategoriesRequest request,
        IReadOnlyList<RegistrationDetail> registrations,
        List<CategoryGenerationWarning> warnings,
        IReadOnlyList<TournamentCategoryPreset> presets)
    {
        var mode = request.GenderMode!.Value;

        var ageGroupRegistrations = registrations
            .Where(r => mode switch
            {
                CategoryGenerationGenderMode.Male => r.AthleteGender == Gender.Male,
                CategoryGenerationGenderMode.Female => r.AthleteGender == Gender.Female,
                CategoryGenerationGenderMode.Mixed => r.AthleteGender is Gender.Male or Gender.Female,
                _ => false
            })
            .ToList();

        var usableRegistrations = ageGroupRegistrations
            .Where(r => r.AthleteWeightKg.HasValue)
            .ToList();

        var ignoredNoWeight = ageGroupRegistrations.Count(r => !r.AthleteWeightKg.HasValue);
        if (ignoredNoWeight > 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningGroupingWithoutWeight", ignoredNoWeight));
        }

        var grouped = usableRegistrations
            .GroupBy(r => mode == CategoryGenerationGenderMode.Mixed ? Gender.Mixed : r.AthleteGender)
            .ToList();

        if (grouped.Count == 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningNoWeightedRegistrations"));
            return [];
        }

        var proposals = new List<GeneratedCategoryProposal>();
        foreach (var group in grouped)
        {
            var targetSize = request.TargetAthletesPerCategory;
            var maxDeviationKg = request.MaxWeightDeviationKg;

            var sortedWeights = group
                .Select(x => x.AthleteWeightKg!.Value)
                .OrderBy(x => x)
                .ToList();

            var (minBirthYear, maxBirthYear) = ResolvePresetBounds(request.AgeGroup.Trim(), group.Key, presets);

            int index = 0;
            decimal? previousLimit = null;
            while (index < sortedWeights.Count)
            {
                var start = index;
                var end = index;

                while (end + 1 < sortedWeights.Count)
                {
                    var count = end - start + 1;
                    if (count >= targetSize)
                    {
                        break;
                    }

                    var nextGap = sortedWeights[end + 1] - sortedWeights[end];
                    if (nextGap > maxDeviationKg)
                    {
                        break;
                    }

                    end++;
                }

                var isLast = end == sortedWeights.Count - 1;
                decimal? limit = isLast ? null : Math.Round(sortedWeights[end], 1, MidpointRounding.AwayFromZero);
                var athleteCount = end - start + 1;

                proposals.Add(new GeneratedCategoryProposal(
                    BuildCategoryName(request.AgeGroup.Trim(), group.Key, limit, previousLimit),
                    request.AgeGroup.Trim(),
                    group.Key,
                    limit,
                    minBirthYear,
                    maxBirthYear,
                    request.MatchDurationSeconds,
                    request.GoldenScoreEnabled,
                    request.GoldenScoreDurationSeconds,
                    athleteCount,
                    "athlete-target"));

                if (limit.HasValue)
                {
                    previousLimit = limit;
                }

                index = end + 1;
            }
        }

        return proposals;
    }

    private static List<StandardPresetRow> MergeToBeMixedRows(IEnumerable<StandardPresetRow> rows)
    {
        var grouped = rows
            .GroupBy(x => x.AgeGroup)
            .Select(g =>
            {
                var hasUnboundedMin = g.Any(x => x.MinBirthYear is null);
                var hasUnboundedMax = g.Any(x => x.MaxBirthYear is null);
                var minBirthYear = hasUnboundedMin ? (int?)null : g.Min(x => x.MinBirthYear!.Value);
                var maxBirthYear = hasUnboundedMax
                    ? (int?)null
                    : g.Select(x => x.MaxBirthYear!.Value).Max();

                var mergedWeights = g
                    .SelectMany(x => x.WeightClassLimitsKg)
                    .Distinct()
                    .OrderBy(x => x ?? decimal.MaxValue)
                    .ToList();

                return new StandardPresetRow(
                    g.Key,
                    Gender.Mixed,
                    minBirthYear,
                    maxBirthYear,
                    g.Max(x => x.DefaultMatchDurationSeconds),
                    mergedWeights);
            })
            .OrderBy(x => x.AgeGroup)
            .ToList();

        return grouped;
    }

    private static string BuildCategoryName(
        string ageGroup,
        Gender gender,
        decimal? weightLimit,
        decimal? heavyFromLimit)
    {
        var genderLabel = gender switch
        {
            Gender.Male => "M",
            Gender.Female => "W",
            Gender.Mixed => "Mixed",
            _ => gender.ToString()
        };

        var weightLabel = weightLimit.HasValue
            ? $"-{weightLimit.Value:0.#} kg"
            : heavyFromLimit.HasValue
                ? $"+{heavyFromLimit.Value:0.#} kg"
                : "+";

        return $"{ageGroup} {genderLabel} {weightLabel}";
    }

    private static int CountMatchingAthletes(
        IReadOnlyList<RegistrationDetail> registrations,
        Gender categoryGender,
        int? minBirthYear,
        int? maxBirthYear,
        decimal? lowerExclusiveLimit,
        decimal? weightLimit)
    {
        return registrations.Count(r =>
            r.AthleteWeightKg.HasValue
            && (categoryGender == Gender.Mixed || r.AthleteGender == categoryGender)
            && (!minBirthYear.HasValue || r.AthleteBirthYear >= minBirthYear.Value)
            && (!maxBirthYear.HasValue || r.AthleteBirthYear <= maxBirthYear.Value)
            && (!lowerExclusiveLimit.HasValue || r.AthleteWeightKg.Value > lowerExclusiveLimit.Value)
            && (!weightLimit.HasValue || r.AthleteWeightKg.Value <= weightLimit.Value));
    }

    private static (int? MinBirthYear, int? MaxBirthYear) ResolvePresetBounds(
        string ageGroup,
        Gender gender,
        IReadOnlyList<TournamentCategoryPreset> presets)
    {
        var matchingPresets = presets
            .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.AgeGroup, ageGroup)
                && (gender == Gender.Mixed || p.Gender == gender))
            .ToList();

        if (matchingPresets.Count == 0)
        {
            return (null, null);
        }

        var minBirthYear = matchingPresets.Any(p => p.MinBirthYear is null)
            ? null
            : matchingPresets.Min(p => p.MinBirthYear);
        var maxBirthYear = matchingPresets.Any(p => p.MaxBirthYear is null)
            ? null
            : matchingPresets.Max(p => p.MaxBirthYear);
        return (minBirthYear, maxBirthYear);
    }

    private static string BuildGroupSettingKey(string ageGroup, CategoryGenerationGenderMode genderMode)
        => $"{ageGroup.Trim().ToUpperInvariant()}|{genderMode}";

    private static CategoryGenerationGenderMode MapGenderToMode(Gender gender)
        => gender switch
        {
            Gender.Male => CategoryGenerationGenderMode.Male,
            Gender.Female => CategoryGenerationGenderMode.Female,
            Gender.Mixed => CategoryGenerationGenderMode.Mixed,
            _ => throw new InvalidOperationException("Unbekanntes Geschlecht.")
        };

    private static string BuildGeneratedNote(string source)
        => $"{GeneratedMarker} source={source}";

    private sealed record StandardPresetRow(
        string AgeGroup,
        Gender Gender,
        int? MinBirthYear,
        int? MaxBirthYear,
        int DefaultMatchDurationSeconds,
        IReadOnlyList<decimal?> WeightClassLimitsKg);

    private sealed record ProposalBuildResult(
        IReadOnlyList<GeneratedCategoryProposal> Categories,
        IReadOnlyList<CategoryGenerationWarning> Warnings,
        IReadOnlyList<Category> CategoriesToReplace,
        IReadOnlyList<GeneratedAthletePreview> AffectedAthletes,
        bool CanApply);
}
