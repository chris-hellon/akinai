using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class BankTransactionDropSourceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankTransactions_SourceFileName_SourceRowNumber",
                schema: "App",
                table: "BankTransactions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_BankTransactions_SourceFileName_SourceRowNumber",
                schema: "App",
                table: "BankTransactions",
                columns: new[] { "SourceFileName", "SourceRowNumber" },
                unique: true);
        }
    }
}
