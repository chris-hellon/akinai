using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDunningReminderInvoiceForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Any database that has deleted an invoice since dunning shipped already holds
            // reminders pointing at nothing, and the constraint below cannot be created
            // while they exist. They have to go first, and deleting them loses nothing:
            // every query reads reminders by invoice id, so a row whose invoice is gone has
            // been unreachable since the moment it was orphaned.
            migrationBuilder.Sql(@"
                DELETE r
                FROM App.DunningReminders AS r
                WHERE NOT EXISTS (SELECT 1 FROM App.Invoices AS i WHERE i.Id = r.InvoiceId);");

            migrationBuilder.AddForeignKey(
                name: "FK_DunningReminders_Invoices_InvoiceId",
                schema: "App",
                table: "DunningReminders",
                column: "InvoiceId",
                principalSchema: "App",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DunningReminders_Invoices_InvoiceId",
                schema: "App",
                table: "DunningReminders");
        }
    }
}
