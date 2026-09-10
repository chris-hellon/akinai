using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTimeEntryBilledHourlyRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BilledHourlyRate",
                schema: "App",
                table: "TimeEntries",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Freeze a rate onto every entry that is already invoiced. Without this
            // they stay null and keep pricing at whatever the job charges today —
            // the behaviour this column exists to stop.
            //
            // The rate used is the one in force now, mirroring the cascade in
            // Job.GetEffectiveHourlyRate (job override → project override → client
            // default). That is the best value still available: the rate an old
            // invoice was actually raised at was never recorded anywhere. Their
            // stored Subtotal/Total are untouched and remain historically correct,
            // so an invoice whose rate changed before this ran can still show rows
            // that do not sum to its header total.
            migrationBuilder.Sql("""
                UPDATE te
                SET te.BilledHourlyRate = COALESCE(
                        j.HourlyRateOverride,
                        p.HourlyRateOverride,
                        c.DefaultHourlyRate,
                        0)
                FROM App.TimeEntries te
                    INNER JOIN App.Jobs j ON j.Id = te.JobId
                    INNER JOIN App.Projects p ON p.Id = j.ProjectId
                    INNER JOIN App.Clients c ON c.Id = p.ClientId
                WHERE te.InvoiceId IS NOT NULL
                  AND te.BilledHourlyRate IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BilledHourlyRate",
                schema: "App",
                table: "TimeEntries");
        }
    }
}
