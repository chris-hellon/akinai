using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOwnershipForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_Users_UserId",
                schema: "App",
                table: "Attachments",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankConnections_Users_UserId",
                schema: "App",
                table: "BankConnections",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BankTransactions_Users_UserId",
                schema: "App",
                table: "BankTransactions",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BusinessProfiles_Users_UserId",
                schema: "App",
                table: "BusinessProfiles",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientLanguageSettings_Users_UserId",
                schema: "App",
                table: "ClientLanguageSettings",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clients_Users_UserId",
                schema: "App",
                table: "Clients",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CurrencyPaymentSettings_Users_UserId",
                schema: "App",
                table: "CurrencyPaymentSettings",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Estimates_Users_UserId",
                schema: "App",
                table: "Estimates",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Users_UserId",
                schema: "App",
                table: "Expenses",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceTemplates_Users_UserId",
                schema: "App",
                table: "InvoiceTemplates",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                schema: "App",
                table: "Notifications",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Users_UserId",
                schema: "App",
                table: "Products",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringExpenses_Users_UserId",
                schema: "App",
                table: "RecurringExpenses",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecurringInvoices_Users_UserId",
                schema: "App",
                table: "RecurringInvoices",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TimeEntries_Users_UserId",
                schema: "App",
                table: "TimeEntries",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserLanguagePreferences_Users_UserId",
                schema: "App",
                table: "UserLanguagePreferences",
                column: "UserId",
                principalSchema: "Identity",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_Users_UserId",
                schema: "App",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_BankConnections_Users_UserId",
                schema: "App",
                table: "BankConnections");

            migrationBuilder.DropForeignKey(
                name: "FK_BankTransactions_Users_UserId",
                schema: "App",
                table: "BankTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_BusinessProfiles_Users_UserId",
                schema: "App",
                table: "BusinessProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientLanguageSettings_Users_UserId",
                schema: "App",
                table: "ClientLanguageSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Clients_Users_UserId",
                schema: "App",
                table: "Clients");

            migrationBuilder.DropForeignKey(
                name: "FK_CurrencyPaymentSettings_Users_UserId",
                schema: "App",
                table: "CurrencyPaymentSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_Estimates_Users_UserId",
                schema: "App",
                table: "Estimates");

            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Users_UserId",
                schema: "App",
                table: "Expenses");

            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceTemplates_Users_UserId",
                schema: "App",
                table: "InvoiceTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                schema: "App",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Products_Users_UserId",
                schema: "App",
                table: "Products");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringExpenses_Users_UserId",
                schema: "App",
                table: "RecurringExpenses");

            migrationBuilder.DropForeignKey(
                name: "FK_RecurringInvoices_Users_UserId",
                schema: "App",
                table: "RecurringInvoices");

            migrationBuilder.DropForeignKey(
                name: "FK_TimeEntries_Users_UserId",
                schema: "App",
                table: "TimeEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_UserLanguagePreferences_Users_UserId",
                schema: "App",
                table: "UserLanguagePreferences");
        }
    }
}
