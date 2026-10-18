using GolfProgress.Domain.Entities;
using GolfProgress.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace GolfProgress.Infrastructure.Persistence;

public class GolfProgressDbContext : DbContext
{

    public GolfProgressDbContext(DbContextOptions<GolfProgressDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Round> Rounds => Set<Round>();
    public DbSet<PracticeSession> PracticeSessions => Set<PracticeSession>();
    public DbSet<Reflection> Reflections => Set<Reflection>();
    public DbSet<UserBaseline> UserBaselines => Set<UserBaseline>();
    public DbSet<FitnessScore> FitnessScores => Set<FitnessScore>();
    public DbSet<MicroWin> MicroWins => Set<MicroWin>();
    public DbSet<MonthlyReview> MonthlyReviews => Set<MonthlyReview>();
    public DbSet<EventLog> Events => Set<EventLog>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ---------- Converters

        // List<string> <-> nvarchar(max) (JSON)

        var stringListConverter = new ValueConverter<List<string>, string>(
            v => SerializeStringList(v),
            v => DeserializeStringList(v)
        );

        // Without a comparer EF compares a List by reference and snapshots the same
        // instance, so editing the list in place (GoalFocus.Add(...)) was never
        // detected and never saved (#12). Compare by contents, snapshot a copy.
        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => StringListsEqual(a, b),
            v => StringListHash(v),
            v => v == null ? null! : v.ToList()
        );

        // JsonDocument <-> nvarchar(max)


        var jsonDocOptions = new JsonDocumentOptions(); // can leave defaults

        var jsonDocumentConverter = new ValueConverter<JsonDocument, string>(
            v => v.RootElement.GetRawText(),
            v => string.IsNullOrWhiteSpace(v)
                ? JsonDocument.Parse("{}", jsonDocOptions)
                : JsonDocument.Parse(v, jsonDocOptions)
        );

        // JsonDocument has no value equality, so EF compared these by reference: any
        // newly parsed document counted as a change, even with identical content
        // (#12). Compare by the JSON text that is actually stored instead.
        // JsonDocument is immutable, so the snapshot can safely be the same instance.
        var jsonDocumentComparer = new ValueComparer<JsonDocument>(
            (a, b) => JsonDocumentsEqual(a, b),
            v => v == null ? 0 : v.RootElement.GetRawText().GetHashCode(),
            v => v
        );


        // ---------- USERS
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.UserId);

            e.HasIndex(x => x.Email).IsUnique();

            e.Property(x => x.Units).HasConversion<string>();
            e.Property(x => x.TrackingMode).HasConversion<string>();
            e.Property(x => x.Language).HasConversion<string>().HasMaxLength(16);

            e.Property(x => x.GoalFocus)
                .HasConversion(stringListConverter, stringListComparer)
                .HasColumnType("nvarchar(max)");

            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("sysutcdatetime()");

            // 1:1 baseline snapshot
            e.HasOne(x => x.Baseline)
                .WithOne(x => x.User)
                .HasForeignKey<UserBaseline>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- ROUNDS
        modelBuilder.Entity<Round>(e =>
        {
            e.ToTable("Rounds");
            e.HasKey(x => x.RoundId);

            e.HasIndex(x => new { x.UserId, x.PlayedAt });

            e.Property(x => x.Source).HasConversion<string>();

            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithMany(u => u.Rounds)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 1:1 FitnessScore
            e.HasOne(x => x.FitnessScore)
                .WithOne(fs => fs.Round)
                .HasForeignKey<FitnessScore>(fs => fs.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- PRACTICE SESSIONS
        modelBuilder.Entity<PracticeSession>(e =>
        {
            e.ToTable("PracticeSessions");
            e.HasKey(x => x.PracticeSessionId);

            e.HasIndex(x => new { x.UserId, x.PracticedAt });

            e.Property(x => x.SessionType).HasConversion<string>();

            e.Property(x => x.Tags)
                .HasConversion(stringListConverter, stringListComparer)
                .HasColumnType("nvarchar(max)");

            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithMany(u => u.PracticeSessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- REFLECTIONS
        modelBuilder.Entity<Reflection>(e =>
        {
            e.ToTable("Reflections");
            e.HasKey(x => x.ReflectionId);

            e.HasIndex(x => x.RoundId);

            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithMany(u => u.Reflections)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // RoundId nullable is OK
            e.HasOne(x => x.Round)
                .WithMany(r => r.Reflections)
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- BASELINES (1:1 with User)
        modelBuilder.Entity<UserBaseline>(e =>
        {
            e.ToTable("UserBaselines");
            e.HasKey(x => x.UserId);

            e.Property(x => x.ConfidenceLevel).HasConversion<string>();
            e.Property(x => x.ComputedAt).HasDefaultValueSql("sysutcdatetime()");
        });

        // ---------- FITNESS SCORES
        modelBuilder.Entity<FitnessScore>(e =>
        {
            e.ToTable("FitnessScores");
            e.HasKey(x => x.FitnessScoreId);

            e.HasIndex(x => new { x.UserId, x.RoundId }).IsUnique();

            e.Property(x => x.ConfidenceLevel).HasConversion<string>();

            e.Property(x => x.ExplanationJson)
                .HasConversion(jsonDocumentConverter, jsonDocumentComparer)
                .HasColumnType("nvarchar(max)");

            e.Property(x => x.ComputedAt).HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(fs => fs.User)
                    .WithMany(u => u.FitnessScores)
                    .HasForeignKey(fs => fs.UserId)
                    .OnDelete(DeleteBehavior.Restrict); // or NoAction

            e.HasOne(fs => fs.Round)
                    .WithOne(r => r.FitnessScore)
                    .HasForeignKey<FitnessScore>(fs => fs.RoundId)
                    .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- MICRO WINS
        modelBuilder.Entity<MicroWin>(e =>
        {
            e.ToTable("MicroWins");
            e.HasKey(x => x.MicroWinId);

            e.HasIndex(x => x.RoundId);
            e.HasIndex(x => new { x.UserId, x.CreatedAt });

            e.Property(x => x.Category).HasConversion<string>();
            e.Property(x => x.Direction).HasConversion<string>();
            e.Property(x => x.ConfidenceLevel).HasConversion<string>();

            e.Property(x => x.EvidenceJson)
                .HasConversion(jsonDocumentConverter, jsonDocumentComparer)
                .HasColumnType("nvarchar(max)");

            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithMany(u => u.MicroWins)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Round)
                .WithMany(r => r.MicroWins)
                .HasForeignKey(x => x.RoundId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- MONTHLY REVIEWS
        modelBuilder.Entity<MonthlyReview>(e =>
        {
            e.ToTable("MonthlyReviews");
            e.HasKey(x => x.MonthlyReviewId);

            e.HasIndex(x => new { x.UserId, x.MonthStart }).IsUnique();

            e.Property(x => x.HighlightsJson)
                .HasConversion(jsonDocumentConverter, jsonDocumentComparer)
                .HasColumnType("nvarchar(max)");

            e.Property(x => x.ComputedAt).HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithMany(u => u.MonthlyReviews)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ---------- EVENTS
        modelBuilder.Entity<EventLog>(e =>
        {
            e.ToTable("Events");
            e.HasKey(x => x.EventLogId);

            e.HasIndex(x => new { x.UserId, x.CreatedAt });
            e.HasIndex(x => x.EventType);

            // The retention purge filters on CreatedAt alone, which the
            // (UserId, CreatedAt) index cannot serve (#20).
            e.HasIndex(x => x.CreatedAt);

            e.Property(x => x.PayloadJson)
                .HasConversion(jsonDocumentConverter, jsonDocumentComparer)
                .HasColumnType("nvarchar(max)");

            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");

            e.HasOne(x => x.User)
                .WithMany(u => u.Events)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshTokens");
            e.HasKey(x => x.RefreshTokenId);

            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ExpiresAt });

            e.Property(x => x.TokenHash)
                .IsRequired()
                .HasMaxLength(200); // SHA256 base64 fits easily

            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.ExpiresAt).IsRequired();

            // Avoid cascade-path issues: don't cascade from User to RefreshTokens.
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Password reset links (#86): looked up by hash, and counted per user for the
        // hourly limit.
        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.ToTable("PasswordResetTokens");
            e.HasKey(x => x.PasswordResetTokenId);

            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.CreatedAt });

            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("sysutcdatetime()");
            e.Property(x => x.ExpiresAt).IsRequired();

            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.General);

    private static string SerializeStringList(List<string> value) =>
        JsonSerializer.Serialize(value, _jsonOptions);

    private static List<string> DeserializeStringList(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? new List<string>()
            : (JsonSerializer.Deserialize<List<string>>(value, _jsonOptions) ?? new List<string>());

    private static bool StringListsEqual(List<string>? a, List<string>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);

    private static int StringListHash(List<string>? value)
    {
        if (value is null) return 0;

        var hash = new HashCode();
        foreach (var item in value) hash.Add(item);
        return hash.ToHashCode();
    }

    private static bool JsonDocumentsEqual(JsonDocument? a, JsonDocument? b) =>
        a is null
            ? b is null
            : b is not null && a.RootElement.GetRawText() == b.RootElement.GetRawText();

}