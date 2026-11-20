using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfProgress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPracticeGoals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PracticeGoalId",
                table: "PracticeSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PracticeGoals",
                columns: table => new
                {
                    PracticeGoalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FocusArea = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SessionType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    TargetSessions = table.Column<short>(type: "smallint", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PracticeGoals", x => x.PracticeGoalId);
                    table.ForeignKey(
                        name: "FK_PracticeGoals_Users_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_PracticeGoals_Users_GolferId",
                        column: x => x.GolferId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PracticeSessions_PracticeGoalId",
                table: "PracticeSessions",
                column: "PracticeGoalId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeGoals_CoachId",
                table: "PracticeGoals",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_PracticeGoals_GolferId_Status_EndsOn",
                table: "PracticeGoals",
                columns: new[] { "GolferId", "Status", "EndsOn" });

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeSessions_PracticeGoals_PracticeGoalId",
                table: "PracticeSessions",
                column: "PracticeGoalId",
                principalTable: "PracticeGoals",
                principalColumn: "PracticeGoalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PracticeSessions_PracticeGoals_PracticeGoalId",
                table: "PracticeSessions");

            migrationBuilder.DropTable(
                name: "PracticeGoals");

            migrationBuilder.DropIndex(
                name: "IX_PracticeSessions_PracticeGoalId",
                table: "PracticeSessions");

            migrationBuilder.DropColumn(
                name: "PracticeGoalId",
                table: "PracticeSessions");
        }
    }
}
