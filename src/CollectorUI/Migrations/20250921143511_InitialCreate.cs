using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CollectorUI.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NamespaceSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolutionPath = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectPath = table.Column<string>(type: "TEXT", nullable: false),
                    Namespace = table.Column<string>(type: "TEXT", nullable: false),
                    IsChecked = table.Column<bool>(type: "INTEGER", nullable: false),
                    SavedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NamespaceSelections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SolutionReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SolutionPath = table.Column<string>(type: "TEXT", nullable: false),
                    LastGeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolutionReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSettings_Key",
                table: "AppSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NamespaceSelections_SolutionPath_ProjectPath_Namespace",
                table: "NamespaceSelections",
                columns: ["SolutionPath", "ProjectPath", "Namespace" ]);

            migrationBuilder.CreateIndex(
                name: "IX_SolutionReports_SolutionPath",
                table: "SolutionReports",
                column: "SolutionPath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "NamespaceSelections");

            migrationBuilder.DropTable(
                name: "SolutionReports");
        }
    }
}
