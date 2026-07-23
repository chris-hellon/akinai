using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Akinai.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SectionId",
                table: "Jobs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectSections_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_SectionId",
                table: "Jobs",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSections_Order",
                table: "ProjectSections",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectSections_ProjectId",
                table: "ProjectSections",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_ProjectSections_SectionId",
                table: "Jobs",
                column: "SectionId",
                principalTable: "ProjectSections",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_ProjectSections_SectionId",
                table: "Jobs");

            migrationBuilder.DropTable(
                name: "ProjectSections");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_SectionId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "Jobs");
        }
    }
}
