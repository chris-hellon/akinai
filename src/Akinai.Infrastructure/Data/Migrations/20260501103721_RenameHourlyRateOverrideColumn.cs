using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameHourlyRateOverrideColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "OverrideHourlyRate",
                table: "Projects",
                newName: "HourlyRateOverride");

            migrationBuilder.RenameColumn(
                name: "OverrideHourlyRate",
                table: "Jobs",
                newName: "HourlyRateOverride");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HourlyRateOverride",
                table: "Projects",
                newName: "OverrideHourlyRate");

            migrationBuilder.RenameColumn(
                name: "HourlyRateOverride",
                table: "Jobs",
                newName: "OverrideHourlyRate");
        }
    }
}
