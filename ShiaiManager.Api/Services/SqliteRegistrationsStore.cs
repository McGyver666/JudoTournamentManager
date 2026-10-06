using ShiaiManager.Api.Data;
using ShiaiManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ShiaiManager.Api.Services;

/// <summary>
/// SQLite-backed registration storage for offline operation.
/// </summary>
public sealed class SqliteRegistrationsStore : IRegistrationsStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteRegistrationsStore> _logger;
    private readonly ICategoryPresetsStore _categoryPresetsStore;

    /// <summary>
    /// Initializes a new store instance.
    /// </summary>
    public SqliteRegistrationsStore(
        AppDbContext dbContext,
        ILogger<SqliteRegistrationsStore> logger,
        ICategoryPresetsStore categoryPresetsStore)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(categoryPresetsStore);
        _dbContext = dbContext;
        _logger = logger;
        _categoryPresetsStore = categoryPresetsStore;
    }

    /// <summary>
    /// Initializes a new store instance.
    /// Compatibility constructor for tests that do not provide a preset store.
    /// </summary>
    public SqliteRegistrationsStore(AppDbContext dbContext, ILogger<SqliteRegistrationsStore> logger)
        : this(dbContext, logger, new SqliteCategoryPresetsStore(dbContext))
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistrationDetail>> GetDetailedAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
#pragma warning disable CS8602 // EF Include() guarantees Athlete and Club are loaded.
        var records = await _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.TournamentId == tournamentId)
            .Include(r => r.Athlete).ThenInclude(a => a.Club)
            .Include(r => r.Category)
            .OrderBy(r => r.Category != null ? r.Category.AgeGroup : string.Empty)
            .ThenBy(r => r.Athlete!.LastName)
            .ThenBy(r => r.Athlete!.FirstName)
            .ToListAsync(cancellationToken);
#pragma warning restore CS8602

        return records.Select(MapToDetail).ToList();
    }

    /// <inheritdoc />
    public async Task<Registration?> GetByIdAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Registrations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken);

        return record is null ? null : MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<Registration?> CreateAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        bool licenseConfirmed,
        CancellationToken cancellationToken)
    {
        return await CreateAtWeighInAsync(
            tournamentId, athleteId, weightKg, null, null, licenseConfirmed, "system", null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Registration?> CreateAtWeighInAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        string? licenseId,
        int? grade,
        bool licenseConfirmed,
        string operatorName,
        string? startAgeGroup,
        CancellationToken cancellationToken)
    {
        var alreadyRegistered = await _dbContext.Registrations.AnyAsync(
            x => x.AthleteId == athleteId && x.TournamentId == tournamentId,
            cancellationToken);

        if (alreadyRegistered)
        {
            _logger.LogWarning(
                "Athlete {AthleteId} is already registered in tournament {TournamentId}.",
                athleteId, tournamentId);
            return null;
        }

        var athlete = await _dbContext.Athletes
            .FirstOrDefaultAsync(a => a.Id == athleteId && a.TournamentId == tournamentId, cancellationToken);

        if (athlete is null)
        {
            _logger.LogWarning(
                "Athlete {AthleteId} not found in tournament {TournamentId}.",
                athleteId, tournamentId);
            return null;
        }

        ApplyWeighInCorrections(athlete, tournamentId, weightKg, licenseId, grade, operatorName);

        var record = new RegistrationRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            AthleteId = athleteId,
            CategoryId = null, // Category assigned later
            StartAgeGroup = NormalizeStartAgeGroup(startAgeGroup),
            LicenseConfirmed = licenseConfirmed,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _dbContext.Registrations.Add(record);
        AddStartAgeGroupAudit(tournamentId, record.Id, null, record.StartAgeGroup, operatorName);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Athlete {AthleteId} registered for tournament {TournamentId} (weight={Weight}kg, licenseConfirmed={LicenseConfirmed}).",
            athleteId, tournamentId, weightKg, licenseConfirmed);
        return MapToModel(record);
    }

    /// <summary>
    /// Backward-compatible overload that accepts a license id parameter.
    /// </summary>
    public async Task<Registration?> CreateAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        string? licenseId,
        bool licenseConfirmed,
        CancellationToken cancellationToken)
    {
        return await CreateAtWeighInAsync(
            tournamentId, athleteId, weightKg, licenseId, null, licenseConfirmed, "system", null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Registration?> CreateWithLicenseCheckAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        string? licenseId,
        bool licenseConfirmed,
        string? dokumeQrUrl,
        string? licenseCheckOverrideReason,
        IDokumePassParser dokumePassParser,
        DateOnly tournamentDate,
        string operatorName,
        int? grade,
        string? startAgeGroup,
        CancellationToken cancellationToken)
    {
        var alreadyRegistered = await _dbContext.Registrations
            .AnyAsync(x => x.AthleteId == athleteId && x.TournamentId == tournamentId, cancellationToken);

        if (alreadyRegistered)
        {
            _logger.LogWarning("Athlete {AthleteId} already registered in tournament {TournamentId}.",
                athleteId, tournamentId);
            return null;
        }

        var athlete = await _dbContext.Athletes
            .FirstOrDefaultAsync(a => a.Id == athleteId && a.TournamentId == tournamentId, cancellationToken);

        if (athlete is null)
        {
            _logger.LogWarning("Athlete {AthleteId} not found in tournament {TournamentId}.",
                athleteId, tournamentId);
            return null;
        }

        bool licenseCheckPassed = false;
        DateOnly? passExpiryDate = null;
        string? licenseNumber = null;

        if (!string.IsNullOrEmpty(dokumeQrUrl))
        {
            var pass = dokumePassParser.ParseQrUrl(dokumeQrUrl);
            if (pass != null)
            {
                var validationResult = dokumePassParser.ValidatePass(pass, tournamentDate,
                    athlete.FirstName, athlete.LastName, athlete.BirthYear);

                if (validationResult.IsValid)
                {
                    licenseCheckPassed = true;
                }
                else if (string.IsNullOrEmpty(licenseCheckOverrideReason))
                {
                    _logger.LogWarning(
                        "License check failed: {Message}", validationResult.Message);
                    return null;
                }

                licenseNumber = pass.PassNumber;
                passExpiryDate = pass.ExpiryDate;
            }
        }

        ApplyWeighInCorrections(athlete, tournamentId, weightKg, licenseId, grade, operatorName);

        var record = new RegistrationRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            AthleteId = athleteId,
            CategoryId = null,
            StartAgeGroup = NormalizeStartAgeGroup(startAgeGroup),
            LicenseConfirmed = licenseConfirmed,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LicenseNumber = licenseNumber,
            PassExpiryDate = passExpiryDate,
            LicenseCheckPassed = licenseCheckPassed,
            LicenseVerifiedAtUtc = DateTimeOffset.UtcNow,
            LicenseVerifiedByUser = operatorName,
            LicenseOverrideReason = licenseCheckOverrideReason
        };

        _dbContext.Registrations.Add(record);
        AddStartAgeGroupAudit(tournamentId, record.Id, null, record.StartAgeGroup, operatorName);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Athlete {AthleteId} registered: weight={Weight}kg, licenseCheckPassed={Passed}",
            athleteId, weightKg, licenseCheckPassed);

        return MapToModel(record);
    }

    /// <summary>
    /// Backward-compatible overload for callers that omitted cancellation token.
    /// </summary>
    public Task<Registration?> CreateWithLicenseCheckAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        string? licenseId,
        bool licenseConfirmed,
        string? dokumeQrUrl,
        string? licenseCheckOverrideReason,
        IDokumePassParser dokumePassParser,
        DateOnly tournamentDate,
        string operatorName)
    {
        return CreateWithLicenseCheckAsync(
            tournamentId,
            athleteId,
            weightKg,
            licenseId,
            licenseConfirmed,
            dokumeQrUrl,
            licenseCheckOverrideReason,
            dokumePassParser,
            tournamentDate,
            operatorName,
            null,
            null,
            CancellationToken.None);
    }

    /// <summary>
    /// Backward-compatible overload for historical parameter order without license id.
    /// </summary>
    public Task<Registration?> CreateWithLicenseCheckAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        bool licenseConfirmed,
        string? dokumeQrUrl,
        string? licenseCheckOverrideReason,
        IDokumePassParser dokumePassParser,
        DateOnly tournamentDate,
        string operatorName,
        CancellationToken cancellationToken)
    {
        return CreateWithLicenseCheckAsync(
            tournamentId,
            athleteId,
            weightKg,
            null,
            licenseConfirmed,
            dokumeQrUrl,
            licenseCheckOverrideReason,
            dokumePassParser,
            tournamentDate,
            operatorName,
            null,
            null,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid registrationId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Registrations
            .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Registration {RegistrationId} not found for deletion.", registrationId);
            return false;
        }

        _dbContext.Registrations.Remove(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Registration {RegistrationId} deleted.", registrationId);
        return true;
    }

    /// <inheritdoc />
    public async Task<Registration?> AssignCategoryAsync(
        Guid registrationId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.Registrations
            .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Registration {RegistrationId} not found for category assignment.", registrationId);
            return null;
        }

        record.CategoryId = categoryId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Registration {RegistrationId} assigned to category {CategoryId}.",
            registrationId, categoryId);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<Registration?> UpdateStartAgeGroupAsync(
        Guid registrationId,
        string? startAgeGroup,
        Guid? categoryId,
        string operatorName,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.Registrations
            .FirstOrDefaultAsync(x => x.Id == registrationId, cancellationToken);
        if (record is null)
        {
            return null;
        }

        var normalizedStartAgeGroup = NormalizeStartAgeGroup(startAgeGroup);
        var previousStartAgeGroup = record.StartAgeGroup;
        record.StartAgeGroup = normalizedStartAgeGroup;
        record.CategoryId = categoryId;

        AddStartAgeGroupAudit(
            record.TournamentId,
            record.Id,
            previousStartAgeGroup,
            normalizedStartAgeGroup,
            operatorName);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<AutoAssignResult> AutoAssignAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        // Load all unassigned registrations including athlete data.
        var unassignedRegistrations = await _dbContext.Registrations
            .Where(r => r.TournamentId == tournamentId && r.CategoryId == null)
#pragma warning disable CS8602 // EF Include() guarantees Athlete is loaded.
            .Include(r => r.Athlete)
#pragma warning restore CS8602
            .ToListAsync(cancellationToken);

        if (unassignedRegistrations.Count == 0)
        {
            return new AutoAssignResult(0, 0, []);
        }

        var presets = await _categoryPresetsStore.GetAllAsync(tournamentId, cancellationToken);

        // Load all unlocked categories for the tournament.
        var categories = await _dbContext.Categories
            .Where(c => c.TournamentId == tournamentId && !c.IsLocked)
            .ToListAsync(cancellationToken);

        int assignedCount = 0;
        var unassigned = new List<UnassignedAthlete>();

        foreach (var registration in unassignedRegistrations)
        {
            var athlete = registration.Athlete!;
            var athleteGender = athlete.Gender;
            var birthYear = athlete.BirthYear;
            var weightKg = athlete.WeightKg;
            var effectiveAgeGroup = AgeGroupResolver.GetEffectiveAgeGroup(
                birthYear,
                Enum.Parse<Gender>(athleteGender),
                registration.StartAgeGroup,
                presets);

            if (effectiveAgeGroup is null)
            {
                var noAgeGroup = AgeGroupMessages.NoPresetForBirthYear;
                unassigned.Add(new UnassignedAthlete(athlete.Id, athlete.FirstName, athlete.LastName, noAgeGroup.Text)
                {
                    ReasonKey = noAgeGroup.Key
                });
                continue;
            }

            // The category birth-year range is only a plausibility hint, so it is not used for matching.
            var candidates = categories
                .Where(c =>
                    AgeGroupResolver.IsSameAgeGroup(c.AgeGroup, effectiveAgeGroup)
                    && (c.Gender == athleteGender || c.Gender == Gender.Mixed.ToString())
                    && (c.WeightClassKg == null || (weightKg != null && weightKg <= c.WeightClassKg)))
                // Prefer exact gender classes over mixed, then smallest fitting weight.
                .OrderBy(c => c.Gender == athleteGender ? 0 : 1)
                // Best fit: lowest weight class first; open weight (null) last.
                .ThenBy(c => c.WeightClassKg == null ? decimal.MaxValue : c.WeightClassKg)
                .ToList();

            if (candidates.Count == 0)
            {
                var reason = BuildNoMatchReason(athlete, weightKg);
                unassigned.Add(new UnassignedAthlete(athlete.Id, athlete.FirstName, athlete.LastName, reason));
                _logger.LogInformation(
                    "Athlete {AthleteId} ({LastName}, {FirstName}) could not be auto-assigned: {Reason}",
                    athlete.Id, athlete.LastName, athlete.FirstName, reason);
                continue;
            }

            registration.CategoryId = candidates[0].Id;
            assignedCount++;
            _logger.LogInformation(
                "Athlete {AthleteId} ({LastName}, {FirstName}) auto-assigned to category {CategoryId} ({CategoryName}).",
                athlete.Id, athlete.LastName, athlete.FirstName, candidates[0].Id, candidates[0].Name);
        }

        if (assignedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Auto-assignment for tournament {TournamentId}: {Assigned} assigned, {Unassigned} unmatched.",
            tournamentId, assignedCount, unassigned.Count);

        return new AutoAssignResult(assignedCount, unassigned.Count, unassigned);
    }

    private static string BuildNoMatchReason(AthleteRecord athlete, decimal? weightKg)
    {
        if (weightKg == null)
        {
            return "Kein Gewicht erfasst und keine offene Gewichtsklasse verfügbar.";
        }

        return $"Keine passende Gewichtsklasse für Jahrgang {athlete.BirthYear}, {athlete.Gender}, {weightKg} kg gefunden.";
    }

    private static Registration MapToModel(RegistrationRecord record) =>
        new Registration(record.Id, record.TournamentId, record.AthleteId, record.CategoryId, record.CreatedAtUtc)
        {
            StartAgeGroup = record.StartAgeGroup
        };

    private static RegistrationDetail MapToDetail(RegistrationRecord r) =>
        new(r.Id,
            r.TournamentId,
            r.AthleteId,
#pragma warning disable CS8602 // Athlete and Club are loaded via Include() in calling queries.
            r.Athlete!.LastName,
            r.Athlete!.FirstName,
            r.Athlete!.BirthYear,
            Enum.Parse<Gender>(r.Athlete!.Gender),
            r.Athlete!.Club!.Name,
            r.CategoryId,
            r.Category?.Name,
            r.Category?.AgeGroup,
            r.Category is not null ? Enum.Parse<Gender>(r.Category.Gender) : null,
            r.Category?.WeightClassKg,
            r.Athlete!.WeightKg,
#pragma warning restore CS8602
            r.LicenseConfirmed,
            r.CreatedAtUtc,
            r.LicenseNumber,
            r.PassExpiryDate,
            r.LicenseCheckPassed,
            r.LicenseVerifiedAtUtc,
            r.LicenseVerifiedByUser)
        {
#pragma warning disable CS8602 // Athlete and Club are loaded via Include() in calling queries.
            AthleteLicenseId = r.Athlete!.LicenseId,
            AthleteGrade = r.Athlete!.Grade,
            StartAgeGroup = r.StartAgeGroup
#pragma warning restore CS8602
        };

    private void ApplyWeighInCorrections(
        AthleteRecord athlete,
        Guid tournamentId,
        decimal weightKg,
        string? licenseId,
        int? grade,
        string operatorName)
    {
        var previousGrade = athlete.Grade;
        var previousLicenseId = athlete.LicenseId;

        athlete.WeightKg = weightKg;
        if (grade.HasValue)
        {
            athlete.Grade = grade.Value;
        }

        if (licenseId is not null)
        {
            athlete.LicenseId = string.IsNullOrWhiteSpace(licenseId) ? null : licenseId.Trim();
        }

        var gradeChanged = athlete.Grade != previousGrade;
        var licenseChanged = !StringComparer.Ordinal.Equals(athlete.LicenseId, previousLicenseId);
        if (!gradeChanged && !licenseChanged)
        {
            return;
        }

        var changes = new List<string>();
        if (gradeChanged)
        {
            changes.Add($"grade {previousGrade?.ToString() ?? "null"}->{athlete.Grade?.ToString() ?? "null"}");
        }

        if (licenseChanged)
        {
            changes.Add("licenseId changed");
        }

        _dbContext.AuditLogs.Add(new AuditLogRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TimestampUtc = DateTimeOffset.UtcNow,
            User = string.IsNullOrWhiteSpace(operatorName) ? "unbekannt" : operatorName.Trim(),
            Action = "AthleteCorrectedAtWeighIn",
            EntityType = "Athlete",
            EntityId = athlete.Id,
            Details = string.Join("; ", changes)
        });
    }

    private void AddStartAgeGroupAudit(
        Guid tournamentId,
        Guid registrationId,
        string? previousStartAgeGroup,
        string? nextStartAgeGroup,
        string operatorName)
    {
        if (AgeGroupResolver.IsSameAgeGroup(previousStartAgeGroup, nextStartAgeGroup))
        {
            return;
        }

        _dbContext.AuditLogs.Add(new AuditLogRecord
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            TimestampUtc = DateTimeOffset.UtcNow,
            User = string.IsNullOrWhiteSpace(operatorName) ? "unbekannt" : operatorName.Trim(),
            Action = "RegistrationStartAgeGroupChanged",
            EntityType = "Registration",
            EntityId = registrationId,
            Details = $"{previousStartAgeGroup ?? "natürlich"}->{nextStartAgeGroup ?? "natürlich"}"
        });
    }

    private static string? NormalizeStartAgeGroup(string? startAgeGroup) =>
        string.IsNullOrWhiteSpace(startAgeGroup) ? null : startAgeGroup.Trim();
}
