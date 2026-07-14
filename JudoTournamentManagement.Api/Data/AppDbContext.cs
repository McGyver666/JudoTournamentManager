using Microsoft.EntityFrameworkCore;

namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Entity Framework database context for the application.
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>
    /// Initializes a new database context instance.
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Tournament records stored in the local database.
    /// </summary>
    public DbSet<TournamentRecord> Tournaments => Set<TournamentRecord>();

    /// <summary>
    /// Tatami (competition area) records stored in the local database.
    /// </summary>
    public DbSet<TatamiRecord> Tatamis => Set<TatamiRecord>();

    /// <summary>
    /// Category (Alters-/Gewichtsklasse) records stored in the local database.
    /// </summary>
    public DbSet<CategoryRecord> Categories => Set<CategoryRecord>();

    /// <summary>
    /// Club records stored in the local database.
    /// </summary>
    public DbSet<ClubRecord> Clubs => Set<ClubRecord>();

    /// <summary>
    /// Athlete records stored in the local database.
    /// </summary>
    public DbSet<AthleteRecord> Athletes => Set<AthleteRecord>();

    /// <summary>
    /// Registration records stored in the local database.
    /// </summary>
    public DbSet<RegistrationRecord> Registrations => Set<RegistrationRecord>();

    /// <summary>
    /// Fight records stored in the local database.
    /// </summary>
    public DbSet<FightRecord> Fights => Set<FightRecord>();

    /// <summary>
    /// Audit log entries for critical actions.
    /// </summary>
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();

    /// <summary>
    /// Local application user accounts.
    /// </summary>
    public DbSet<UserAccountRecord> UserAccounts => Set<UserAccountRecord>();

    /// <summary>
    /// Issued authentication sessions/tokens.
    /// </summary>
    public DbSet<AuthSessionRecord> AuthSessions => Set<AuthSessionRecord>();

    /// <summary>
    /// Tournament category preset rows for the standard-class generation assistant.
    /// </summary>
    public DbSet<CategoryPresetRecord> CategoryPresets => Set<CategoryPresetRecord>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        var tournament = modelBuilder.Entity<TournamentRecord>();
        tournament.ToTable("Tournaments");
        tournament.HasKey(x => x.Id);
        tournament.Property(x => x.Name).IsRequired().HasMaxLength(120);
        tournament.Property(x => x.Venue).IsRequired().HasMaxLength(160);
        tournament.Property(x => x.Organizer).IsRequired().HasMaxLength(120);
        tournament.Property(x => x.AccentSideColor).IsRequired().HasMaxLength(10).HasDefaultValue("Blue");

        var tatami = modelBuilder.Entity<TatamiRecord>();
        tatami.ToTable("Tatamis");
        tatami.HasKey(x => x.Id);
        tatami.Property(x => x.Name).IsRequired().HasMaxLength(80);
        tatami.HasOne(x => x.Tournament)
              .WithMany()
              .HasForeignKey(x => x.TournamentId)
              .OnDelete(DeleteBehavior.Restrict);

        var category = modelBuilder.Entity<CategoryRecord>();
        category.ToTable("Categories");
        category.HasKey(x => x.Id);
        category.Property(x => x.Name).IsRequired().HasMaxLength(120);
        category.Property(x => x.AgeGroup).IsRequired().HasMaxLength(40);
        category.Property(x => x.Gender).IsRequired().HasMaxLength(20);
        category.Property(x => x.RulesetNotes).HasMaxLength(500);
        category.Property(x => x.MatchDurationSeconds).HasDefaultValue(300);
        category.Property(x => x.GoldenScoreEnabled).HasDefaultValue(false);
        category.Property(x => x.GoldenScoreDurationSeconds).HasDefaultValue(180);
        category.Property(x => x.DrawFormat).HasMaxLength(40);
        category.HasOne(x => x.Tournament)
                .WithMany()
                .HasForeignKey(x => x.TournamentId)
                .OnDelete(DeleteBehavior.Restrict);

        var club = modelBuilder.Entity<ClubRecord>();
        club.ToTable("Clubs");
        club.HasKey(x => x.Id);
        club.Property(x => x.Name).IsRequired().HasMaxLength(120);
        club.HasOne(x => x.Tournament)
            .WithMany()
            .HasForeignKey(x => x.TournamentId)
            .OnDelete(DeleteBehavior.Restrict);

        var athlete = modelBuilder.Entity<AthleteRecord>();
        athlete.ToTable("Athletes");
        athlete.HasKey(x => x.Id);
        athlete.Property(x => x.FirstName).IsRequired().HasMaxLength(60);
        athlete.Property(x => x.LastName).IsRequired().HasMaxLength(60);
        athlete.Property(x => x.Gender).IsRequired().HasMaxLength(20);
        athlete.Property(x => x.LicenseId).HasMaxLength(40);
        athlete.Property(x => x.WeightKg);
        athlete.Property(x => x.Grade).IsRequired().HasDefaultValue(1);
        athlete.HasOne(x => x.Tournament)
               .WithMany()
               .HasForeignKey(x => x.TournamentId)
               .OnDelete(DeleteBehavior.Restrict);
        athlete.HasOne(x => x.Club)
               .WithMany()
               .HasForeignKey(x => x.ClubId)
               .OnDelete(DeleteBehavior.Restrict);

        var registration = modelBuilder.Entity<RegistrationRecord>();
        registration.ToTable("Registrations");
        registration.HasKey(x => x.Id);
        registration.HasIndex(x => new { x.AthleteId, x.TournamentId }).IsUnique();
        registration.Property(x => x.LicenseNumber).HasMaxLength(40);
        registration.Property(x => x.LicenseVerifiedByUser).HasMaxLength(120);
        registration.Property(x => x.LicenseOverrideReason).HasMaxLength(200);
        registration.HasOne(x => x.Tournament)
                    .WithMany()
                    .HasForeignKey(x => x.TournamentId)
                    .OnDelete(DeleteBehavior.Restrict);
        registration.HasOne(x => x.Athlete)
                    .WithMany()
                    .HasForeignKey(x => x.AthleteId)
                    .OnDelete(DeleteBehavior.Restrict);
        registration.HasOne(x => x.Category)
                    .WithMany()
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);

        var fight = modelBuilder.Entity<FightRecord>();
        fight.ToTable("Fights");
        fight.HasKey(x => x.Id);
        fight.Property(x => x.BracketType).IsRequired().HasMaxLength(20);
        fight.Property(x => x.Status).IsRequired().HasMaxLength(20);
        fight.Property(x => x.WhiteSourceOutcome).HasMaxLength(10);
        fight.Property(x => x.BlueSourceOutcome).HasMaxLength(10);
        fight.HasOne(x => x.Tournament)
             .WithMany()
             .HasForeignKey(x => x.TournamentId)
             .OnDelete(DeleteBehavior.Restrict);
        fight.HasOne(x => x.Category)
             .WithMany()
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);
        fight.HasOne<TatamiRecord>()
             .WithMany()
             .HasForeignKey(x => x.TatamiId)
             .OnDelete(DeleteBehavior.SetNull);

           fight.Property(x => x.OsaeKomiSide).HasMaxLength(10);

        var auditLog = modelBuilder.Entity<AuditLogRecord>();
        auditLog.ToTable("AuditLogs");
        auditLog.HasKey(x => x.Id);
        auditLog.Property(x => x.User).IsRequired().HasMaxLength(120);
        auditLog.Property(x => x.Action).IsRequired().HasMaxLength(60);
        auditLog.Property(x => x.EntityType).IsRequired().HasMaxLength(60);
        auditLog.Property(x => x.Details).HasMaxLength(1000);
        auditLog.HasIndex(x => x.TournamentId);

        var userAccount = modelBuilder.Entity<UserAccountRecord>();
        userAccount.ToTable("UserAccounts");
        userAccount.HasKey(x => x.Id);
        userAccount.Property(x => x.UserName).IsRequired().HasMaxLength(120);
        userAccount.Property(x => x.NormalizedUserName).IsRequired().HasMaxLength(120);
        userAccount.Property(x => x.Role).IsRequired().HasMaxLength(40);
        userAccount.Property(x => x.PasswordHash).IsRequired();
        userAccount.Property(x => x.PasswordSalt).IsRequired();
        userAccount.HasIndex(x => x.NormalizedUserName).IsUnique();

        var authSession = modelBuilder.Entity<AuthSessionRecord>();
        authSession.ToTable("AuthSessions");
        authSession.HasKey(x => x.Id);
        authSession.Property(x => x.TokenHash).IsRequired();
        authSession.HasOne(x => x.UserAccount)
            .WithMany()
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
        authSession.HasIndex(x => x.TokenHash).IsUnique();
        authSession.HasIndex(x => new { x.UserAccountId, x.ExpiresAtUtc });

        var categoryPreset = modelBuilder.Entity<CategoryPresetRecord>();
        categoryPreset.ToTable("CategoryPresets");
        categoryPreset.HasKey(x => x.Id);
        categoryPreset.Property(x => x.AgeGroup).IsRequired().HasMaxLength(40);
        categoryPreset.Property(x => x.Gender).IsRequired().HasMaxLength(20);
        categoryPreset.Property(x => x.WeightClassLimitsJson).IsRequired().HasMaxLength(1000);
        categoryPreset.Property(x => x.DefaultMatchDurationSeconds).HasDefaultValue(240);
        categoryPreset.HasOne(x => x.Tournament)
                      .WithMany()
                      .HasForeignKey(x => x.TournamentId)
                      .OnDelete(DeleteBehavior.Cascade);
        categoryPreset.HasIndex(x => x.TournamentId);
    }
}
