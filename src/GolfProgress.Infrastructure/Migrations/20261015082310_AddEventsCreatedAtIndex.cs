using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GolfProgress.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEventsCreatedAtIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Events_CreatedAt",
                table: "Events",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_CreatedAt",
                table: "Events");
        }
    }
}
