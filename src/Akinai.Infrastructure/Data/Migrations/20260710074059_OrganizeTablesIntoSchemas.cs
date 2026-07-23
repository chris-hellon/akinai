using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class OrganizeTablesIntoSchemas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Identity");

            migrationBuilder.EnsureSchema(
                name: "App");

            migrationBuilder.RenameTable(
                name: "UserLanguagePreferences",
                newName: "UserLanguagePreferences",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "TimeEntries",
                newName: "TimeEntries",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "RecurringInvoices",
                newName: "RecurringInvoices",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "RecurringInvoiceLineItems",
                newName: "RecurringInvoiceLineItems",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "RecurringExpenses",
                newName: "RecurringExpenses",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "ProjectSections",
                newName: "ProjectSections",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Projects",
                newName: "Projects",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Products",
                newName: "Products",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "JobTasks",
                newName: "JobTasks",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Jobs",
                newName: "Jobs",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "InvoiceTemplates",
                newName: "InvoiceTemplates",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Invoices",
                newName: "Invoices",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "InvoiceRecurringInvoice",
                newName: "InvoiceRecurringInvoice",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "InvoiceLineItems",
                newName: "InvoiceLineItems",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Expenses",
                newName: "Expenses",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Estimates",
                newName: "Estimates",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "EstimateLineItems",
                newName: "EstimateLineItems",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "DigitalSignatures",
                newName: "DigitalSignatures",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "CurrencyPaymentSettings",
                newName: "CurrencyPaymentSettings",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Clients",
                newName: "Clients",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "ClientLanguageSettings",
                newName: "ClientLanguageSettings",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "BusinessProfiles",
                newName: "BusinessProfiles",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "BankTransactions",
                newName: "BankTransactions",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "Attachments",
                newName: "Attachments",
                newSchema: "App");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                newName: "AspNetUserTokens",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                newName: "AspNetUsers",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                newName: "AspNetUserRoles",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AspNetUserPasskeys",
                newName: "AspNetUserPasskeys",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                newName: "AspNetUserLogins",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                newName: "AspNetUserClaims",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                newName: "AspNetRoles",
                newSchema: "Identity");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                newName: "AspNetRoleClaims",
                newSchema: "Identity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "UserLanguagePreferences",
                schema: "App",
                newName: "UserLanguagePreferences");

            migrationBuilder.RenameTable(
                name: "TimeEntries",
                schema: "App",
                newName: "TimeEntries");

            migrationBuilder.RenameTable(
                name: "RecurringInvoices",
                schema: "App",
                newName: "RecurringInvoices");

            migrationBuilder.RenameTable(
                name: "RecurringInvoiceLineItems",
                schema: "App",
                newName: "RecurringInvoiceLineItems");

            migrationBuilder.RenameTable(
                name: "RecurringExpenses",
                schema: "App",
                newName: "RecurringExpenses");

            migrationBuilder.RenameTable(
                name: "ProjectSections",
                schema: "App",
                newName: "ProjectSections");

            migrationBuilder.RenameTable(
                name: "Projects",
                schema: "App",
                newName: "Projects");

            migrationBuilder.RenameTable(
                name: "Products",
                schema: "App",
                newName: "Products");

            migrationBuilder.RenameTable(
                name: "JobTasks",
                schema: "App",
                newName: "JobTasks");

            migrationBuilder.RenameTable(
                name: "Jobs",
                schema: "App",
                newName: "Jobs");

            migrationBuilder.RenameTable(
                name: "InvoiceTemplates",
                schema: "App",
                newName: "InvoiceTemplates");

            migrationBuilder.RenameTable(
                name: "Invoices",
                schema: "App",
                newName: "Invoices");

            migrationBuilder.RenameTable(
                name: "InvoiceRecurringInvoice",
                schema: "App",
                newName: "InvoiceRecurringInvoice");

            migrationBuilder.RenameTable(
                name: "InvoiceLineItems",
                schema: "App",
                newName: "InvoiceLineItems");

            migrationBuilder.RenameTable(
                name: "Expenses",
                schema: "App",
                newName: "Expenses");

            migrationBuilder.RenameTable(
                name: "Estimates",
                schema: "App",
                newName: "Estimates");

            migrationBuilder.RenameTable(
                name: "EstimateLineItems",
                schema: "App",
                newName: "EstimateLineItems");

            migrationBuilder.RenameTable(
                name: "DigitalSignatures",
                schema: "App",
                newName: "DigitalSignatures");

            migrationBuilder.RenameTable(
                name: "CurrencyPaymentSettings",
                schema: "App",
                newName: "CurrencyPaymentSettings");

            migrationBuilder.RenameTable(
                name: "Clients",
                schema: "App",
                newName: "Clients");

            migrationBuilder.RenameTable(
                name: "ClientLanguageSettings",
                schema: "App",
                newName: "ClientLanguageSettings");

            migrationBuilder.RenameTable(
                name: "BusinessProfiles",
                schema: "App",
                newName: "BusinessProfiles");

            migrationBuilder.RenameTable(
                name: "BankTransactions",
                schema: "App",
                newName: "BankTransactions");

            migrationBuilder.RenameTable(
                name: "Attachments",
                schema: "App",
                newName: "Attachments");

            migrationBuilder.RenameTable(
                name: "AspNetUserTokens",
                schema: "Identity",
                newName: "AspNetUserTokens");

            migrationBuilder.RenameTable(
                name: "AspNetUsers",
                schema: "Identity",
                newName: "AspNetUsers");

            migrationBuilder.RenameTable(
                name: "AspNetUserRoles",
                schema: "Identity",
                newName: "AspNetUserRoles");

            migrationBuilder.RenameTable(
                name: "AspNetUserPasskeys",
                schema: "Identity",
                newName: "AspNetUserPasskeys");

            migrationBuilder.RenameTable(
                name: "AspNetUserLogins",
                schema: "Identity",
                newName: "AspNetUserLogins");

            migrationBuilder.RenameTable(
                name: "AspNetUserClaims",
                schema: "Identity",
                newName: "AspNetUserClaims");

            migrationBuilder.RenameTable(
                name: "AspNetRoles",
                schema: "Identity",
                newName: "AspNetRoles");

            migrationBuilder.RenameTable(
                name: "AspNetRoleClaims",
                schema: "Identity",
                newName: "AspNetRoleClaims");
        }
    }
}
