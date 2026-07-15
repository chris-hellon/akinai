using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTemplateDefaultColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PrimaryColor",
                schema: "App",
                table: "InvoiceTemplates",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#0e5c66",
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldDefaultValue: "#4F46E5");

            migrationBuilder.AlterColumn<string>(
                name: "AccentColor",
                schema: "App",
                table: "InvoiceTemplates",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#059669",
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldDefaultValue: "#6366F1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PrimaryColor",
                schema: "App",
                table: "InvoiceTemplates",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#4F46E5",
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldDefaultValue: "#0e5c66");

            migrationBuilder.AlterColumn<string>(
                name: "AccentColor",
                schema: "App",
                table: "InvoiceTemplates",
                type: "nvarchar(7)",
                maxLength: 7,
                nullable: false,
                defaultValue: "#6366F1",
                oldClrType: typeof(string),
                oldType: "nvarchar(7)",
                oldMaxLength: 7,
                oldDefaultValue: "#059669");
        }
    }
}
