using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScanToPayAndPaymentInsightsPlanFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PaymentInsights",
                schema: "App",
                table: "PlanSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ScanToPay",
                schema: "App",
                table: "PlanSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Paid tiers get both. The columns default to false for existing rows, and
            // PlanSettingsSeeder is insert-only by design — an edited row survives a
            // restart — so without this the Pro row keeps a plan it was never meant to
            // have. Keyed on price rather than name so a renamed tier still gets it.
            migrationBuilder.Sql(@"
                UPDATE App.PlanSettings
                SET ScanToPay = 1, PaymentInsights = 1
                WHERE MonthlyPrice > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentInsights",
                schema: "App",
                table: "PlanSettings");

            migrationBuilder.DropColumn(
                name: "ScanToPay",
                schema: "App",
                table: "PlanSettings");
        }
    }
}
