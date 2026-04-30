using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfProgress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timezone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Units = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HandicapIndex = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Typical18Score = table.Column<int>(type: "int", nullable: true),
                    GoalFocus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TrackingMode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Events",
                columns: table => new
                {
                    EventLogId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RelatedId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Events", x => x.EventLogId);
                    table.ForeignKey(
                        name: "FK_Events_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyReviews",
                columns: table => new
                {
                    MonthlyReviewId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MonthStart = table.Column<DateOnly>(type: "date", nullable: false),
                    FitnessAvg = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FitnessChange = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HighlightsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FocusNextMonth = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyReviews", x => x.MonthlyReviewId);
                    table.ForeignKey(
                        name: "FK_MonthlyReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PracticeSessions",
                columns: table => new
                {
                    PracticeSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PracticedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    DurationMinutes = table.Column<short>(type: "smallint", nullable: true),
                    SessionType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VenueName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrimaryGoal = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeSessions", x => x.PracticeSessionId);
                    table.ForeignKey(
                        name: "FK_PracticeSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    RoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlayedAt = table.Column<DateOnly>(type: "date", nullable: false),
                    Holes = table.Column<short>(type: "smallint", nullable: false),
                    CourseName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TeeName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalStrokes = table.Column<short>(type: "smallint", nullable: false),
                    TotalPutts = table.Column<short>(type: "smallint", nullable: true),
                    Penalties = table.Column<short>(type: "smallint", nullable: true),
                    FairwaysHit = table.Column<short>(type: "smallint", nullable: true),
                    Gir = table.Column<short>(type: "smallint", nullable: true),
                    ThreePutts = table.Column<short>(type: "smallint", nullable: true),
                    BlowUpHoles = table.Column<short>(type: "smallint", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.RoundId);
                    table.ForeignKey(
                        name: "FK_Rounds_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserBaselines",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WindowRounds = table.Column<short>(type: "smallint", nullable: false),
                    AvgScore = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ScoreStdDev = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgPutts = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgPenalties = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgThreePutts = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgBlowUpHoles = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgFairwaysHit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    AvgGir = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CompletenessRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserBaselines", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserBaselines_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FitnessScores",
                columns: table => new
                {
                    FitnessScoreId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ScoreTotal = table.Column<short>(type: "smallint", nullable: false),
                    Execution = table.Column<short>(type: "smallint", nullable: false),
                    DamageControl = table.Column<short>(type: "smallint", nullable: false),
                    Consistency = table.Column<short>(type: "smallint", nullable: false),
                    Habits = table.Column<short>(type: "smallint", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExplanationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FitnessScores", x => x.FitnessScoreId);
                    table.ForeignKey(
                        name: "FK_FitnessScores_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "RoundId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FitnessScores_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MicroWins",
                columns: table => new
                {
                    MicroWinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Metric = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Direction = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DeltaValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ConfidenceLevel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MicroWins", x => x.MicroWinId);
                    table.ForeignKey(
                        name: "FK_MicroWins_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "RoundId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MicroWins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reflections",
                columns: table => new
                {
                    ReflectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FocusRating = table.Column<short>(type: "smallint", nullable: true),
                    MoodRating = table.Column<short>(type: "smallint", nullable: true),
                    FrustrationSpillover = table.Column<bool>(type: "bit", nullable: true),
                    WhatWorked = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WhatToRepeat = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OneChangeNext = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reflections", x => x.ReflectionId);
                    table.ForeignKey(
                        name: "FK_Reflections_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "RoundId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reflections_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Events_EventType",
                table: "Events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_Events_UserId_CreatedAt",
                table: "Events",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FitnessScores_RoundId",
                table: "FitnessScores",
                column: "RoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FitnessScores_UserId_RoundId",
                table: "FitnessScores",
                columns: new[] { "UserId", "RoundId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MicroWins_RoundId",
                table: "MicroWins",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_MicroWins_UserId_CreatedAt",
                table: "MicroWins",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyReviews_UserId_MonthStart",
                table: "MonthlyReviews",
                columns: new[] { "UserId", "MonthStart" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_UserId_PracticedAt",
                table: "PracticeSessions",
                columns: new[] { "UserId", "PracticedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reflections_RoundId",
                table: "Reflections",
                column: "RoundId");

            migrationBuilder.CreateIndex(
                name: "IX_Reflections_UserId",
                table: "Reflections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_UserId_PlayedAt",
                table: "Rounds",
                columns: new[] { "UserId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[Email] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Events");

            migrationBuilder.DropTable(
                name: "FitnessScores");

            migrationBuilder.DropTable(
                name: "MicroWins");

            migrationBuilder.DropTable(
                name: "MonthlyReviews");

            migrationBuilder.DropTable(
                name: "PracticeSessions");

            migrationBuilder.DropTable(
                name: "Reflections");

            migrationBuilder.DropTable(
                name: "UserBaselines");

            migrationBuilder.DropTable(
                name: "Rounds");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
