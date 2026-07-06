using JudoTournamentManagement.Api.Contracts;
using JudoTournamentManagement.Api.Models;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Default category generation implementation for the assistant workflow.
/// </summary>
public sealed class CategoryGenerationService : ICategoryGenerationService
{
    private const string GeneratedMarker = "[AUTO_GENERATED_2026]";

    private static readonly IReadOnlyList<StandardPresetRow> StandardPresets =
    [
        // Female
        new("U11", Gender.Female, 2016, 2018, 120, [22m, 24m, 26m, 28m, 30m, 33m, 36m, 40m, 44m, 48m, null]),
        new("U13", Gender.Female, 2014, 2016, 180, [27m, 30m, 33m, 36m, 40m, 44m, 48m, 52m, 57m, null]),
        new("U15", Gender.Female, 2012, 2014, 180, [33m, 36m, 40m, 44m, 48m, 52m, 57m, 63m, null]),
        new("U18", Gender.Female, 2009, 2011, 240, [40m, 44m, 48m, 52m, 57m, 63m, 70m, 78m, null]),
        new("U21", Gender.Female, 2006, 2009, 240, [48m, 52m, 57m, 63m, 70m, 78m, null]),
        new("Frauen", Gender.Female, 2009, null, 240, [48m, 52m, 57m, 63m, 70m, 78m, null]),

        // Male
        new("U11", Gender.Male, 2016, 2018, 120, [23m, 25m, 27m, 29m, 31m, 34m, 37m, 40m, 43m, 46m, null]),
        new("U13", Gender.Male, 2014, 2016, 180, [28m, 31m, 34m, 37m, 40m, 43m, 46m, 50m, 55m, null]),
        new("U15", Gender.Male, 2012, 2014, 180, [34m, 37m, 40m, 43m, 46m, 50m, 55m, 60m, 66m, null]),
        new("U18", Gender.Male, 2009, 2011, 240, [46m, 50m, 55m, 60m, 66m, 73m, 81m, 90m, null]),
        new("U21", Gender.Male, 2006, 2009, 240, [60m, 66m, 73m, 81m, 90m, 100m, null]),
        new("Männer", Gender.Male, 2009, null, 240, [60m, 66m, 73m, 81m, 90m, 100m, null]),
    ];

    private readonly ICategoriesStore _categoriesStore;
    private readonly IRegistrationsStore _registrationsStore;
    private readonly ILogger<CategoryGenerationService> _logger;

    public CategoryGenerationService(
        ICategoriesStore categoriesStore,
        IRegistrationsStore registrationsStore,
        ILogger<CategoryGenerationService> logger)
    {
        ArgumentNullException.ThrowIfNull(categoriesStore);
        ArgumentNullException.ThrowIfNull(registrationsStore);
        ArgumentNullException.ThrowIfNull(logger);

        _categoriesStore = categoriesStore;
        _registrationsStore = registrationsStore;
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
            proposals.Warnings);
    }

    public async Task<CategoryGenerationApplyResponse> ApplyAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        var proposals = await BuildProposalsAsync(tournamentId, request, cancellationToken);
        var warnings = proposals.Warnings.ToList();

        const int deletedCount = 0;
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
            warnings.Add($"{skippedDuplicateCount} Kategorien wurden wegen Duplikaten übersprungen.");
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

        var warnings = new List<string>();
        var registrations = await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken);

        var categories = request.WeightMode switch
        {
            CategoryGenerationWeightMode.StandardClasses => BuildStandardProposals(request, registrations, warnings),
            CategoryGenerationWeightMode.AthletesByTargetSize => BuildAthleteDrivenProposals(request, registrations, warnings),
            _ => throw new InvalidOperationException("Unbekannte Gewichtsklassen-Strategie.")
        };

        var uniqueCategories = categories
            .GroupBy(c => new { c.AgeGroup, c.Gender, c.WeightClassKg })
            .Select(g => g.First())
            .OrderBy(c => c.AgeGroup)
            .ThenBy(c => c.Gender)
            .ThenBy(c => c.WeightClassKg ?? decimal.MaxValue)
            .ToList();

        return new ProposalBuildResult(uniqueCategories, warnings);
    }

    private static void ValidateRequest(GenerateCategoriesRequest request)
    {
        if (request.GenderMode is null)
        {
            throw new InvalidOperationException("Der Geschlechtsmodus ist erforderlich.");
        }

        if (request.WeightMode is null)
        {
            throw new InvalidOperationException("Die Gewichtsklassen-Strategie ist erforderlich.");
        }

        if (request.MinBirthYear.HasValue
            && request.MaxBirthYear.HasValue
            && request.MinBirthYear.Value > request.MaxBirthYear.Value)
        {
            throw new InvalidOperationException("Mindest-Geburtsjahr darf nicht größer als Höchst-Geburtsjahr sein.");
        }
    }

    private List<GeneratedCategoryProposal> BuildStandardProposals(
        GenerateCategoriesRequest request,
        IReadOnlyList<RegistrationDetail> registrations,
        List<string> warnings)
    {
        var mode = request.GenderMode!.Value;

        var rows = mode switch
        {
            CategoryGenerationGenderMode.Male => StandardPresets.Where(x => x.Gender == Gender.Male).ToList(),
            CategoryGenerationGenderMode.Female => StandardPresets.Where(x => x.Gender == Gender.Female).ToList(),
            CategoryGenerationGenderMode.Mixed => BuildMixedPresetRows(),
            _ => throw new InvalidOperationException("Unbekannter Geschlechtsmodus.")
        };

        rows = rows
            .Where(x => IsPresetRelevantForRequestedYears(
                x.MinBirthYear,
                x.MaxBirthYear,
                request.MinBirthYear,
                request.MaxBirthYear))
            .ToList();

        if (rows.Count == 0)
        {
            warnings.Add("Keine Standardklassen für den gewählten Jahrgangsbereich gefunden.");
            return [];
        }

        var proposals = new List<GeneratedCategoryProposal>();
        foreach (var row in rows)
        {
            decimal? previousLimit = null;
            foreach (var limit in row.WeightClassLimitsKg)
            {
                var (minYear, maxYear) = ClampRange(row.MinBirthYear, row.MaxBirthYear, request.MinBirthYear, request.MaxBirthYear);
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
                    "standard-2026"));

                if (limit.HasValue)
                {
                    previousLimit = limit;
                }
            }
        }

        return proposals;
    }

    private List<GeneratedCategoryProposal> BuildAthleteDrivenProposals(
        GenerateCategoriesRequest request,
        IReadOnlyList<RegistrationDetail> registrations,
        List<string> warnings)
    {
        var mode = request.GenderMode!.Value;

        var usableRegistrations = registrations
            .Where(r => r.AthleteWeightKg.HasValue)
            .Where(r => !request.MinBirthYear.HasValue || r.AthleteBirthYear >= request.MinBirthYear.Value)
            .Where(r => !request.MaxBirthYear.HasValue || r.AthleteBirthYear <= request.MaxBirthYear.Value)
            .Where(r => mode switch
            {
                CategoryGenerationGenderMode.Male => r.AthleteGender == Gender.Male,
                CategoryGenerationGenderMode.Female => r.AthleteGender == Gender.Female,
                CategoryGenerationGenderMode.Mixed => r.AthleteGender is Gender.Male or Gender.Female,
                _ => false
            })
            .ToList();

        var ignoredNoWeight = registrations.Count(r => !r.AthleteWeightKg.HasValue);
        if (ignoredNoWeight > 0)
        {
            warnings.Add($"{ignoredNoWeight} Athleten ohne Gewicht wurden bei der gewichtsbasierten Generierung ignoriert.");
        }

        var grouped = usableRegistrations
            .Select(r => new
            {
                Registration = r,
                AgeGroup = ResolveAgeGroup(r.AthleteBirthYear, r.AthleteGender, mode)
            })
            .Where(x => x.AgeGroup is not null)
            .GroupBy(
                x => new
                {
                    AgeGroup = x.AgeGroup!,
                    Gender = mode == CategoryGenerationGenderMode.Mixed
                        ? Gender.Mixed
                        : x.Registration.AthleteGender
                },
                x => x.Registration)
            .ToList();

        if (grouped.Count == 0)
        {
            warnings.Add("Keine Athleten mit passenden Daten für die gewichtsbasierte Generierung gefunden.");
            return [];
        }

        var settingMap = request.GroupSettings
            .Where(x => x.GenderMode.HasValue)
            .ToDictionary(
                x => BuildGroupSettingKey(x.AgeGroup.Trim(), x.GenderMode!.Value),
                x => x,
                StringComparer.OrdinalIgnoreCase);

        var proposals = new List<GeneratedCategoryProposal>();
        foreach (var group in grouped)
        {
            var settingsKey = BuildGroupSettingKey(group.Key.AgeGroup, MapGenderToMode(group.Key.Gender));
            settingMap.TryGetValue(settingsKey, out var settings);

            var targetSize = settings?.TargetAthletesPerCategory ?? 8;
            var maxDeviationKg = settings?.MaxWeightDeviationKg ?? 2m;

            var sortedWeights = group
                .Select(x => x.AthleteWeightKg!.Value)
                .OrderBy(x => x)
                .ToList();

            var (minBirthYear, maxBirthYear) = ResolveGroupBounds(group.Key.AgeGroup, request.MinBirthYear, request.MaxBirthYear);

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
                    BuildCategoryName(group.Key.AgeGroup, group.Key.Gender, limit, previousLimit),
                    group.Key.AgeGroup,
                    group.Key.Gender,
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

    private static List<StandardPresetRow> BuildMixedPresetRows()
    {
        var grouped = StandardPresets
            .GroupBy(x => NormalizeMixedAgeGroupLabel(x.AgeGroup))
            .Select(g =>
            {
                var minBirthYear = g
                    .Where(x => x.MinBirthYear.HasValue)
                    .Select(x => x.MinBirthYear!.Value)
                    .DefaultIfEmpty()
                    .Min();

                var hasUnboundedMax = g.Any(x => x.MaxBirthYear is null);
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
                    minBirthYear == 0 ? null : minBirthYear,
                    maxBirthYear,
                    g.Max(x => x.DefaultMatchDurationSeconds),
                    mergedWeights);
            })
            .OrderBy(x => x.AgeGroup)
            .ToList();

        return grouped;
    }

    private static string NormalizeMixedAgeGroupLabel(string label)
    {
        if (label.Equals("Männer", StringComparison.OrdinalIgnoreCase)
            || label.Equals("Frauen", StringComparison.OrdinalIgnoreCase))
        {
            return "Senioren";
        }

        return label;
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

    private static bool Overlaps(int? aMin, int? aMax, int? bMin, int? bMax)
    {
        var left = Math.Max(aMin ?? int.MinValue, bMin ?? int.MinValue);
        var right = Math.Min(aMax ?? int.MaxValue, bMax ?? int.MaxValue);
        return left <= right;
    }

    private static bool IsPresetRelevantForRequestedYears(
        int? presetMin,
        int? presetMax,
        int? requestMin,
        int? requestMax)
    {
        if (!requestMin.HasValue && !requestMax.HasValue)
        {
            return true;
        }

        // If the user provided an explicit window, keep only bounded presets
        // that fully cover this window. This avoids broad overlap suggestions
        // such as Seniors when a narrow youth window is selected.
        if (requestMin.HasValue && requestMax.HasValue)
        {
            return presetMin.HasValue
                   && presetMax.HasValue
                   && presetMin.Value <= requestMin.Value
                   && presetMax.Value >= requestMax.Value;
        }

        return Overlaps(presetMin, presetMax, requestMin, requestMax);
    }

    private static (int? Min, int? Max) ClampRange(int? baseMin, int? baseMax, int? reqMin, int? reqMax)
    {
        var min = MaxNullable(baseMin, reqMin);
        var max = MinNullable(baseMax, reqMax);
        return (min, max);
    }

    private static int? MaxNullable(int? a, int? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        return Math.Max(a.Value, b.Value);
    }

    private static int? MinNullable(int? a, int? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        return Math.Min(a.Value, b.Value);
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

    private static string? ResolveAgeGroup(int birthYear, Gender athleteGender, CategoryGenerationGenderMode mode)
    {
        var rows = mode switch
        {
            CategoryGenerationGenderMode.Male => StandardPresets.Where(x => x.Gender == Gender.Male),
            CategoryGenerationGenderMode.Female => StandardPresets.Where(x => x.Gender == Gender.Female),
            CategoryGenerationGenderMode.Mixed => StandardPresets.Where(x => x.Gender == athleteGender),
            _ => []
        };

        var match = rows.FirstOrDefault(x =>
            (!x.MinBirthYear.HasValue || birthYear >= x.MinBirthYear.Value)
            && (!x.MaxBirthYear.HasValue || birthYear <= x.MaxBirthYear.Value));

        if (match is null)
        {
            return null;
        }

        return mode == CategoryGenerationGenderMode.Mixed
            ? NormalizeMixedAgeGroupLabel(match.AgeGroup)
            : match.AgeGroup;
    }

    private static (int? MinBirthYear, int? MaxBirthYear) ResolveGroupBounds(
        string ageGroup,
        int? requestMinBirthYear,
        int? requestMaxBirthYear)
    {
        var matches = StandardPresets
            .Where(x => NormalizeMixedAgeGroupLabel(x.AgeGroup).Equals(ageGroup, StringComparison.OrdinalIgnoreCase)
                     || x.AgeGroup.Equals(ageGroup, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            return (requestMinBirthYear, requestMaxBirthYear);
        }

        int? min = matches
            .Where(x => x.MinBirthYear.HasValue)
            .Select(x => x.MinBirthYear!.Value)
            .DefaultIfEmpty()
            .Min();

        if (min == 0)
        {
            min = null;
        }

        int? max = matches.Any(x => x.MaxBirthYear is null)
            ? null
            : matches.Max(x => x.MaxBirthYear!.Value);

        return ClampRange(min, max, requestMinBirthYear, requestMaxBirthYear);
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
        IReadOnlyList<string> Warnings);
}
