using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPlanSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlanSettings",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plan = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Tagline = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    CurrencySymbol = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    InvoicesPerMonth = table.Column<int>(type: "int", nullable: true),
                    MultiCurrency = table.Column<bool>(type: "bit", nullable: false),
                    AllLanguages = table.Column<bool>(type: "bit", nullable: false),
                    BankImport = table.Column<bool>(type: "bit", nullable: false),
                    RecurringBilling = table.Column<bool>(type: "bit", nullable: false),
                    Estimates = table.Column<bool>(type: "bit", nullable: false),
                    CustomTemplates = table.Column<bool>(type: "bit", nullable: false),
                    RemoveBranding = table.Column<bool>(type: "bit", nullable: false),
                    AiSpendLimitUsd = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: true),
                    Highlights = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanSettings_Plan",
                schema: "App",
                table: "PlanSettings",
                column: "Plan",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlanSettings",
                schema: "App");
        }
    }
}
