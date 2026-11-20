using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfProgress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrackFeedbackReadsPerItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A read was per golfer; it can't be told which item it was for, so they start again.
            // At worst some feedback shows as new once more.
            migrationBuilder.Sql("DELETE FROM [FeedbackReads]");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeedbackReads",
                table: "FeedbackReads");

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                table: "FeedbackReads",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "TargetId",
                table: "FeedbackReads",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeedbackReads",
                table: "FeedbackReads",
                columns: new[] { "UserId", "GolferId", "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Several reads per golfer can't become one.
            migrationBuilder.Sql("DELETE FROM [FeedbackReads]");

            migrationBuilder.DropPrimaryKey(
                name: "PK_FeedbackReads",
                table: "FeedbackReads");

            migrationBuilder.DropColumn(
                name: "TargetType",
                table: "FeedbackReads");

            migrationBuilder.DropColumn(
                name: "TargetId",
                table: "FeedbackReads");

            migrationBuilder.AddPrimaryKey(
                name: "PK_FeedbackReads",
                table: "FeedbackReads",
                columns: new[] { "UserId", "GolferId" });
        }
    }
}
