using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomaticRemindersPlanFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AutomaticReminders",
                schema: "App",
                table: "PlanSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Same reasoning as AddScanToPayAndPaymentInsightsPlanFlags: the column
            // defaults to false for existing rows and the seeder is insert-only, so the
            // Pro row would otherwise come out of this migration unable to send the
            // reminders its own highlights advertise. Keyed on price, not name.
            //
            // Deliberately backfills the flag only. The Pro price also changed in this
            // release (£9 to £14) and that is NOT applied here — a price is something an
            // admin may have set on purpose through /admin/plans, and a migration must
            // not silently overwrite that. A new column has no such intent behind it.
            migrationBuilder.Sql(@"
                UPDATE App.PlanSettings
                SET AutomaticReminders = 1
                WHERE MonthlyPrice > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutomaticReminders",
                schema: "App",
                table: "PlanSettings");
        }
    }
}
