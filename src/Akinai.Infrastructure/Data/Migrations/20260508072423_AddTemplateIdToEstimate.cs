using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateIdToEstimate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Estimates",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Estimates_TemplateId",
                table: "Estimates",
                column: "TemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_Estimates_InvoiceTemplates_TemplateId",
                table: "Estimates",
                column: "TemplateId",
                principalTable: "InvoiceTemplates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Estimates_InvoiceTemplates_TemplateId",
                table: "Estimates");

            migrationBuilder.DropIndex(
                name: "IX_Estimates_TemplateId",
                table: "Estimates");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Estimates");
        }
    }
}
