using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssistantPlans_MatchKey_Status",
                table: "AssistantPlans");

            migrationBuilder.DropIndex(
                name: "IX_AssistantNoAnswers_ClusterKey",
                table: "AssistantNoAnswers");

            migrationBuilder.AddColumn<Guid>(
                name: "AppId",
                table: "AssistantReportedAnswers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppId",
                table: "AssistantPlans",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppId",
                table: "AssistantNoAnswers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AppId",
                table: "AssistantInteractions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Apps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    BaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PersonaPrompt = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantReportedAnswers_AppId",
                table: "AssistantReportedAnswers",
                column: "AppId");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantPlans_AppId_MatchKey_Status",
                table: "AssistantPlans",
                columns: new[] { "AppId", "MatchKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoAnswers_AppId_ClusterKey",
                table: "AssistantNoAnswers",
                columns: new[] { "AppId", "ClusterKey" },
                unique: true,
                filter: "[AppId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractions_AppId",
                table: "AssistantInteractions",
                column: "AppId");

            migrationBuilder.CreateIndex(
                name: "IX_Apps_Code",
                table: "Apps",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Apps");

            migrationBuilder.DropIndex(
                name: "IX_AssistantReportedAnswers_AppId",
                table: "AssistantReportedAnswers");

            migrationBuilder.DropIndex(
                name: "IX_AssistantPlans_AppId_MatchKey_Status",
                table: "AssistantPlans");

            migrationBuilder.DropIndex(
                name: "IX_AssistantNoAnswers_AppId_ClusterKey",
                table: "AssistantNoAnswers");

            migrationBuilder.DropIndex(
                name: "IX_AssistantInteractions_AppId",
                table: "AssistantInteractions");

            migrationBuilder.DropColumn(
                name: "AppId",
                table: "AssistantReportedAnswers");

            migrationBuilder.DropColumn(
                name: "AppId",
                table: "AssistantPlans");

            migrationBuilder.DropColumn(
                name: "AppId",
                table: "AssistantNoAnswers");

            migrationBuilder.DropColumn(
                name: "AppId",
                table: "AssistantInteractions");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantPlans_MatchKey_Status",
                table: "AssistantPlans",
                columns: new[] { "MatchKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoAnswers_ClusterKey",
                table: "AssistantNoAnswers",
                column: "ClusterKey",
                unique: true);
        }
    }
}
