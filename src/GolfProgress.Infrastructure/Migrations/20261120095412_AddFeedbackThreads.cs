using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfProgress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedbackThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FeedbackMessages",
                columns: table => new
                {
                    FeedbackMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TargetType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    EditedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackMessages", x => x.FeedbackMessageId);
                    table.ForeignKey(
                        name: "FK_FeedbackMessages_Users_AuthorId",
                        column: x => x.AuthorId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_FeedbackMessages_Users_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                    table.ForeignKey(
                        name: "FK_FeedbackMessages_Users_GolferId",
                        column: x => x.GolferId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FeedbackReads",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReadUpTo = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeedbackReads", x => new { x.UserId, x.GolferId });
                    table.ForeignKey(
                        name: "FK_FeedbackReads_Users_GolferId",
                        column: x => x.GolferId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FeedbackReads_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackMessages_AuthorId",
                table: "FeedbackMessages",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackMessages_CoachId",
                table: "FeedbackMessages",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackMessages_GolferId_CoachId_CreatedAt",
                table: "FeedbackMessages",
                columns: new[] { "GolferId", "CoachId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackMessages_GolferId_TargetType_TargetId_CreatedAt",
                table: "FeedbackMessages",
                columns: new[] { "GolferId", "TargetType", "TargetId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackReads_GolferId",
                table: "FeedbackReads",
                column: "GolferId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FeedbackMessages");

            migrationBuilder.DropTable(
                name: "FeedbackReads");
        }
    }
}
