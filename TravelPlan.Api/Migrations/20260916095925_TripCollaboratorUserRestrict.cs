using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPlan.Api.Migrations
{
    /// <inheritdoc />
    public partial class TripCollaboratorUserRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripCollaborators_Users_UserId",
                table: "TripCollaborators");

            migrationBuilder.AddForeignKey(
                name: "FK_TripCollaborators_Users_UserId",
                table: "TripCollaborators",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TripCollaborators_Users_UserId",
                table: "TripCollaborators");

            migrationBuilder.AddForeignKey(
                name: "FK_TripCollaborators_Users_UserId",
                table: "TripCollaborators",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
