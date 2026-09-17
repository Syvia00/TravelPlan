using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPlan.Api.Migrations
{
    /// <inheritdoc />
    public partial class DestinationFksRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accommodations_Destinations_DestinationId",
                table: "Accommodations");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanItems_Destinations_DestinationId",
                table: "PlanItems");

            migrationBuilder.AddForeignKey(
                name: "FK_Accommodations_Destinations_DestinationId",
                table: "Accommodations",
                column: "DestinationId",
                principalTable: "Destinations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanItems_Destinations_DestinationId",
                table: "PlanItems",
                column: "DestinationId",
                principalTable: "Destinations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Accommodations_Destinations_DestinationId",
                table: "Accommodations");

            migrationBuilder.DropForeignKey(
                name: "FK_PlanItems_Destinations_DestinationId",
                table: "PlanItems");

            migrationBuilder.AddForeignKey(
                name: "FK_Accommodations_Destinations_DestinationId",
                table: "Accommodations",
                column: "DestinationId",
                principalTable: "Destinations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanItems_Destinations_DestinationId",
                table: "PlanItems",
                column: "DestinationId",
                principalTable: "Destinations",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
