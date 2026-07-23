using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTranslationCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TranslationCache",
                schema: "App",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    SourceLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    TranslatedText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TranslationCache", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TranslationCache_SourceHash_SourceLanguage_TargetLanguage",
                schema: "App",
                table: "TranslationCache",
                columns: new[] { "SourceHash", "SourceLanguage", "TargetLanguage" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TranslationCache",
                schema: "App");
        }
    }
}
