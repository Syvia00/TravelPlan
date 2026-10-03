using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelPlan.Api.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class FixExchangeRatePrecisionAndUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRates_BaseCurrency_QuoteCurrency",
                table: "ExchangeRates",
                columns: new[] { "BaseCurrency", "QuoteCurrency" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExchangeRates_BaseCurrency_QuoteCurrency",
                table: "ExchangeRates");
        }
    }
}
