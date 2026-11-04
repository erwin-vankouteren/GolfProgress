using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfProgress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CoachInvitations",
                columns: table => new
                {
                    CoachInvitationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShareReflections = table.Column<bool>(type: "bit", nullable: false),
                    SharePractice = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeclinedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachInvitations", x => x.CoachInvitationId);
                    table.ForeignKey(
                        name: "FK_CoachInvitations_Users_GolferId",
                        column: x => x.GolferId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CoachLinks",
                columns: table => new
                {
                    CoachLinkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GolferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CoachId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShareReflections = table.Column<bool>(type: "bit", nullable: false),
                    SharePractice = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "sysutcdatetime()"),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndedBy = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoachLinks", x => x.CoachLinkId);
                    table.ForeignKey(
                        name: "FK_CoachLinks_Users_CoachId",
                        column: x => x.CoachId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CoachLinks_Users_GolferId",
                        column: x => x.GolferId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CoachInvitations_GolferId_Email",
                table: "CoachInvitations",
                columns: new[] { "GolferId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_CoachInvitations_TokenHash",
                table: "CoachInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CoachLinks_CoachId",
                table: "CoachLinks",
                column: "CoachId");

            migrationBuilder.CreateIndex(
                name: "IX_CoachLinks_GolferId_CoachId",
                table: "CoachLinks",
                columns: new[] { "GolferId", "CoachId" },
                unique: true,
                filter: "[EndedAt] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CoachInvitations");

            migrationBuilder.DropTable(
                name: "CoachLinks");
        }
    }
}
