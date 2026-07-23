using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceTemplateId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TemplateId",
                table: "Invoices",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_InvoiceTemplates_TemplateId",
                table: "Invoices",
                column: "TemplateId",
                principalTable: "InvoiceTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_InvoiceTemplates_TemplateId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TemplateId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Invoices");
        }
    }
}
