using System.Diagnostics;
using ShiaiManager.Api.Contracts;
using ShiaiManager.Api.Models;
using Microsoft.Extensions.Logging;

namespace ShiaiManager.Api.Services;

/// <summary>
/// Generates the categories of one age group and gender scope from the tournament presets.
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
            AffectedRegistrations = proposals.AffectedRegistrations,
            CanApply = proposals.CanApply
        };
    }

    public async Task<CategoryGenerationApplyResponse> ApplyAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        var proposals = await BuildProposalsAsync(tournamentId, request, cancellationToken);
        if (!proposals.CanApply)
        {
            throw new LocalizedOperationException(AgeGroupMessages.CategoriesCannotBeReplaced);
        }

        var newCategories = proposals.Categories
            .Select(proposal => new NewCategory(
                proposal.Name,
                proposal.AgeGroup,
                proposal.Gender,
                proposal.WeightClassKg,
                proposal.MinBirthYear,
                proposal.MaxBirthYear,
                $"{GeneratedMarker} source={proposal.Source}",
                proposal.MatchDurationSeconds,
                proposal.GoldenScoreEnabled,
                proposal.GoldenScoreDurationSeconds))
            .ToList();

        var result = await _categoriesStore.ReplaceAsync(
            tournamentId,
            proposals.CategoriesToReplace.Select(c => c.Id).ToList(),
            newCategories,
            cancellationToken)
            // A category may have been drawn or locked between preview and apply.
            ?? throw new LocalizedOperationException(AgeGroupMessages.CategoriesCannotBeReplaced);

        var warnings = proposals.Warnings.ToList();
        if (result.SkippedDuplicateCount > 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningDuplicatesSkipped", result.SkippedDuplicateCount));
        }

        _logger.LogInformation(
            "Category generation applied for tournament {TournamentId}: created={Created}, deleted={Deleted}, duplicateSkipped={DuplicateSkipped}.",
            tournamentId,
            result.Created.Count,
            result.DeletedCount,
            result.SkippedDuplicateCount);

        return new CategoryGenerationApplyResponse(
            result.Created.Count,
            result.DeletedCount,
            result.SkippedDuplicateCount,
            result.Created,
            warnings);
    }

    private async Task<ProposalBuildResult> BuildProposalsAsync(
        Guid tournamentId,
        GenerateCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var ageGroup = request.AgeGroup.Trim();
        var genderMode = request.GenderMode!.Value;

        var warnings = new List<CategoryGenerationWarning>();
        var registrations = await _registrationsStore.GetDetailedAsync(tournamentId, cancellationToken);
        var presets = await _categoryPresetsStore.GetAllAsync(tournamentId, cancellationToken);

        var matchingPresets = presets
            .Where(p => AgeGroupResolver.IsSameAgeGroup(p.AgeGroup, ageGroup) && MatchesGenderMode(p.Gender, genderMode))
            .ToList();

        if (genderMode == CategoryGenerationGenderMode.Mixed
            && (!matchingPresets.Any(p => p.Gender == Gender.Male)
                || !matchingPresets.Any(p => p.Gender == Gender.Female)))
        {
            throw new LocalizedOperationException(AgeGroupMessages.MixedRequiresBothGenders);
        }

        if (matchingPresets.Count == 0)
        {
            throw new LocalizedOperationException(AgeGroupMessages.PresetNotFound);
        }

        if (request.WeightMode == CategoryGenerationWeightMode.StandardClasses
            && matchingPresets.Any(p => p.WeightClassLimitsKg.Count == 0))
        {
            throw new LocalizedOperationException(AgeGroupMessages.PresetWithoutStandardClasses);
        }

        var registrationsWithoutPreset = registrations.Count(r => AgeGroupResolver.GetEffectiveAgeGroup(r, presets) is null);
        if (registrationsWithoutPreset > 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningRegistrationsWithoutPreset", registrationsWithoutPreset));
        }

        var eligibleRegistrations = registrations
            .Where(r => MatchesGenderMode(r.AthleteGender, genderMode)
                && AgeGroupResolver.IsSameAgeGroup(AgeGroupResolver.GetEffectiveAgeGroup(r, presets), ageGroup))
            .ToList();

        if (request.WeightMode == CategoryGenerationWeightMode.StandardClasses)
        {
            var registrationsWithoutWeight = eligibleRegistrations.Count(r => !r.AthleteWeightKg.HasValue);
            if (registrationsWithoutWeight > 0)
            {
                warnings.Add(new CategoryGenerationWarning("categories.warningStandardClassesWithoutWeight", registrationsWithoutWeight));
            }
        }

        var categoryGender = ToCategoryGender(genderMode);
        var (minBirthYear, maxBirthYear) = MergeBirthYearBounds(matchingPresets);
        var target = new ProposalTarget(ageGroup, categoryGender, minBirthYear, maxBirthYear);
        var categories = (request.WeightMode switch
        {
            CategoryGenerationWeightMode.StandardClasses =>
                BuildStandardProposals(target, matchingPresets, request, eligibleRegistrations),
            CategoryGenerationWeightMode.AthletesByTargetSize =>
                BuildAthleteDrivenProposals(target, request, eligibleRegistrations, warnings),
            _ => throw new UnreachableException()
        })
            .GroupBy(c => c.WeightClassKg)
            .Select(g => g.First())
            .OrderBy(c => c.WeightClassKg ?? decimal.MaxValue)
            .ToList();

        var existingCategories = await _categoriesStore.GetAllAsync(tournamentId, cancellationToken);
        var categoriesToReplace = existingCategories
            .Where(c => AgeGroupResolver.IsSameAgeGroup(c.AgeGroup, ageGroup) && c.Gender == categoryGender)
            .ToList();
        var categoryIdsWithFights = await _categoriesStore.GetIdsWithFightsAsync(tournamentId, cancellationToken);
        var canApply = !categoriesToReplace.Any(c =>
            c.IsLocked || c.DrawFormat.HasValue || categoryIdsWithFights.Contains(c.Id));
        if (!canApply)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningCategoriesLocked"));
        }

        var affectedRegistrations = eligibleRegistrations
            .Select(registration => new GeneratedRegistrationPreview(
                registration.Id,
                registration.AthleteFirstName,
                registration.AthleteLastName,
                registration.AthleteBirthYear,
                registration.AthleteGender,
                registration.AthleteWeightKg,
                registration.StartAgeGroup))
            .ToList();

        return new ProposalBuildResult(categories, warnings, categoriesToReplace, affectedRegistrations, canApply);
    }

    private static void ValidateRequest(GenerateCategoriesRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AgeGroup))
        {
            throw new LocalizedOperationException(AgeGroupMessages.AgeGroupRequired);
        }

        if (request.GenderMode is not { } genderMode || !Enum.IsDefined(genderMode))
        {
            throw new LocalizedOperationException(AgeGroupMessages.GenderModeRequired);
        }

        if (request.WeightMode is not { } weightMode || !Enum.IsDefined(weightMode))
        {
            throw new LocalizedOperationException(AgeGroupMessages.WeightModeRequired);
        }

        if (request.TargetAthletesPerCategory is < 2 or > 64)
        {
            throw new LocalizedOperationException(AgeGroupMessages.TargetSizeOutOfRange);
        }

        if (request.MaxWeightDeviationKg is < 0.1m or > 50m)
        {
            throw new LocalizedOperationException(AgeGroupMessages.MaxWeightDeviationOutOfRange);
        }
    }

    /// <summary>
    /// Whether an athlete or preset of <paramref name="gender"/> takes part in a run with <paramref name="mode"/>.
    /// </summary>
    private static bool MatchesGenderMode(Gender gender, CategoryGenerationGenderMode mode) =>
        mode == CategoryGenerationGenderMode.Mixed
            ? gender is Gender.Male or Gender.Female
            : gender == ToCategoryGender(mode);

    private static Gender ToCategoryGender(CategoryGenerationGenderMode mode) =>
        mode switch
        {
            CategoryGenerationGenderMode.Male => Gender.Male,
            CategoryGenerationGenderMode.Female => Gender.Female,
            CategoryGenerationGenderMode.Mixed => Gender.Mixed,
            _ => throw new UnreachableException()
        };

    private static (int? MinBirthYear, int? MaxBirthYear) MergeBirthYearBounds(
        IReadOnlyList<TournamentCategoryPreset> presets)
    {
        var minBirthYear = presets.Any(p => p.MinBirthYear is null) ? null : presets.Min(p => p.MinBirthYear);
        var maxBirthYear = presets.Any(p => p.MaxBirthYear is null) ? null : presets.Max(p => p.MaxBirthYear);
        return (minBirthYear, maxBirthYear);
    }

    private static List<GeneratedCategoryProposal> BuildStandardProposals(
        ProposalTarget target,
        IReadOnlyList<TournamentCategoryPreset> presets,
        GenerateCategoriesRequest request,
        IReadOnlyList<RegistrationDetail> registrations)
    {
        // Mixed runs combine the male and female weight limits of the same age group.
        var weightLimits = presets
            .SelectMany(p => p.WeightClassLimitsKg)
            .Distinct()
            .OrderBy(limit => limit ?? decimal.MaxValue)
            .ToList();

        var proposals = new List<GeneratedCategoryProposal>();
        decimal? previousLimit = null;
        foreach (var limit in weightLimits)
        {
            proposals.Add(target.ToProposal(
                limit,
                previousLimit,
                CountAthletesInWeightBand(registrations, previousLimit, limit),
                request,
                "standard"));

            if (limit.HasValue)
            {
                previousLimit = limit;
            }
        }

        return proposals;
    }

    private static List<GeneratedCategoryProposal> BuildAthleteDrivenProposals(
        ProposalTarget target,
        GenerateCategoriesRequest request,
        IReadOnlyList<RegistrationDetail> registrations,
        List<CategoryGenerationWarning> warnings)
    {
        var ignoredNoWeight = registrations.Count(r => !r.AthleteWeightKg.HasValue);
        if (ignoredNoWeight > 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningGroupingWithoutWeight", ignoredNoWeight));
        }

        var sortedWeights = registrations
            .Where(r => r.AthleteWeightKg.HasValue)
            .Select(r => r.AthleteWeightKg!.Value)
            .OrderBy(x => x)
            .ToList();

        if (sortedWeights.Count == 0)
        {
            warnings.Add(new CategoryGenerationWarning("categories.warningNoWeightedRegistrations"));
            return [];
        }

        var proposals = new List<GeneratedCategoryProposal>();
        var index = 0;
        decimal? previousLimit = null;
        while (index < sortedWeights.Count)
        {
            var start = index;
            var end = index;

            while (end + 1 < sortedWeights.Count
                && end - start + 1 < request.TargetAthletesPerCategory
                && sortedWeights[end + 1] - sortedWeights[end] <= request.MaxWeightDeviationKg)
            {
                end++;
            }

            var isLast = end == sortedWeights.Count - 1;
            decimal? limit = isLast ? null : Math.Round(sortedWeights[end], 1, MidpointRounding.AwayFromZero);
            proposals.Add(target.ToProposal(limit, previousLimit, end - start + 1, request, "athlete-target"));

            if (limit.HasValue)
            {
                previousLimit = limit;
            }

            index = end + 1;
        }

        return proposals;
    }

    private static int CountAthletesInWeightBand(
        IReadOnlyList<RegistrationDetail> registrations,
        decimal? lowerExclusiveLimit,
        decimal? weightLimit)
    {
        return registrations.Count(r =>
            r.AthleteWeightKg.HasValue
            && (!lowerExclusiveLimit.HasValue || r.AthleteWeightKg.Value > lowerExclusiveLimit.Value)
            && (!weightLimit.HasValue || r.AthleteWeightKg.Value <= weightLimit.Value));
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

    /// <summary>
    /// Age group, gender and preset birth-year range shared by all proposals of one generation run.
    /// </summary>
    private sealed record ProposalTarget(string AgeGroup, Gender Gender, int? MinBirthYear, int? MaxBirthYear)
    {
        public GeneratedCategoryProposal ToProposal(
            decimal? weightLimit,
            decimal? previousLimit,
            int estimatedAthletes,
            GenerateCategoriesRequest request,
            string source) =>
            new(
                BuildCategoryName(AgeGroup, Gender, weightLimit, previousLimit),
                AgeGroup,
                Gender,
                weightLimit,
                MinBirthYear,
                MaxBirthYear,
                request.MatchDurationSeconds,
                request.GoldenScoreEnabled,
                request.GoldenScoreDurationSeconds,
                estimatedAthletes,
                source);
    }

    private sealed record ProposalBuildResult(
        IReadOnlyList<GeneratedCategoryProposal> Categories,
        IReadOnlyList<CategoryGenerationWarning> Warnings,
        IReadOnlyList<Category> CategoriesToReplace,
        IReadOnlyList<GeneratedRegistrationPreview> AffectedRegistrations,
        bool CanApply);
}
