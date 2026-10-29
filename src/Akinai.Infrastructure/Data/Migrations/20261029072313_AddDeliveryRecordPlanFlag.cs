using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeliveryRecordPlanFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DeliveryRecord",
                schema: "App",
                table: "PlanSettings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The column defaults to false and the seeder is insert-only, so without this
            // the Pro row would come out of the migration unable to show the delivery
            // record its own highlights now advertise. Keyed on price rather than name,
            // the same way every earlier flag backfill is: the tier a deployment calls
            // "Pro" is editable, what it charges is the thing that makes it paid.
            migrationBuilder.Sql(@"
                UPDATE App.PlanSettings
                SET DeliveryRecord = 1
                WHERE MonthlyPrice > 0;");

            // The pricing page lists the stored highlights, not the compiled ones, so
            // without this the Pro card advertises everything except the feature just
            // added to it.
            //
            // Appends rather than overwrites, which is why this is safe where resetting
            // the price would not be: the highlights are a textarea an admin may have
            // edited through /admin/plans, and adding a line they can delete is a far
            // smaller imposition than discarding their wording wholesale. Skipped
            // entirely where the line is already there, so re-running or a row seeded
            // from current code cannot produce it twice. It lands at the end rather than
            // beside the reminders bullet it sits next to in Plans.cs — ordering is not
            // worth hand-written string surgery on someone else's copy.
            migrationBuilder.Sql(@"
                UPDATE App.PlanSettings
                SET Highlights = Highlights + CHAR(10) +
                    N'A delivery record for every invoice — proof of what you sent, and when they opened it'
                WHERE DeliveryRecord = 1
                  AND Highlights NOT LIKE N'%delivery record%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryRecord",
                schema: "App",
                table: "PlanSettings");
        }
    }
}
