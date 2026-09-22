using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPlan.Api.Migrations.SqlServer
{
    /// <inheritdoc />
    public partial class AddTripCollaboratorUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripCollaborators_TripId",
                table: "TripCollaborators");

            migrationBuilder.CreateIndex(
                name: "IX_TripCollaborators_TripId_UserId",
                table: "TripCollaborators",
                columns: new[] { "TripId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TripCollaborators_TripId_UserId",
                table: "TripCollaborators");

            migrationBuilder.CreateIndex(
                name: "IX_TripCollaborators_TripId",
                table: "TripCollaborators",
                column: "TripId");
        }
    }
}
