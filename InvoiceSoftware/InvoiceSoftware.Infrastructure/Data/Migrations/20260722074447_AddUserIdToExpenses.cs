using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceSoftware.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToExpenses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "App",
                table: "RecurringExpenses",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                schema: "App",
                table: "Expenses",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringExpenses_UserId",
                schema: "App",
                table: "RecurringExpenses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_UserId",
                schema: "App",
                table: "Expenses",
                column: "UserId");

            // ----------------------------------------------------------------
            // Backfill. The column is added with default '' so the ALTER can run
            // against existing rows; without this step every pre-existing expense
            // would be owned by nobody and invisible to every user.
            //
            // Derive the owner from the linked client, then the project's client.
            // Anything still unowned is a "general" expense that never had either
            // — those can only be attributed when the database has exactly one
            // user, which is checked explicitly rather than assumed.
            // ----------------------------------------------------------------
            migrationBuilder.Sql("""
                UPDATE e SET e.UserId = c.UserId
                FROM App.Expenses e
                JOIN App.Clients c ON c.Id = e.ClientId
                WHERE e.UserId = '' AND e.ClientId IS NOT NULL;

                UPDATE e SET e.UserId = c.UserId
                FROM App.Expenses e
                JOIN App.Projects p ON p.Id = e.ProjectId
                JOIN App.Clients  c ON c.Id = p.ClientId
                WHERE e.UserId = '' AND e.ProjectId IS NOT NULL;

                UPDATE re SET re.UserId = c.UserId
                FROM App.RecurringExpenses re
                JOIN App.Clients c ON c.Id = re.ClientId
                WHERE re.UserId = '' AND re.ClientId IS NOT NULL;

                UPDATE re SET re.UserId = c.UserId
                FROM App.RecurringExpenses re
                JOIN App.Projects p ON p.Id = re.ProjectId
                JOIN App.Clients  c ON c.Id = p.ClientId
                WHERE re.UserId = '' AND re.ProjectId IS NOT NULL;

                IF (SELECT COUNT(*) FROM [Identity].[Users]) = 1
                BEGIN
                    DECLARE @onlyUser nvarchar(450) = (SELECT TOP 1 Id FROM [Identity].[Users]);
                    UPDATE App.Expenses          SET UserId = @onlyUser WHERE UserId = '';
                    UPDATE App.RecurringExpenses SET UserId = @onlyUser WHERE UserId = '';
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringExpenses_UserId",
                schema: "App",
                table: "RecurringExpenses");

            migrationBuilder.DropIndex(
                name: "IX_Expenses_UserId",
                schema: "App",
                table: "Expenses");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "App",
                table: "RecurringExpenses");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "App",
                table: "Expenses");
        }
    }
}
