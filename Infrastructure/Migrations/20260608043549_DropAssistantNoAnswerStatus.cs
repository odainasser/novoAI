using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropAssistantNoAnswerStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssistantNoAnswers_TriageStatus",
                table: "AssistantNoAnswers");

            migrationBuilder.DropColumn(
                name: "TriageNote",
                table: "AssistantNoAnswers");

            migrationBuilder.DropColumn(
                name: "TriageStatus",
                table: "AssistantNoAnswers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TriageNote",
                table: "AssistantNoAnswers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TriageStatus",
                table: "AssistantNoAnswers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoAnswers_TriageStatus",
                table: "AssistantNoAnswers",
                column: "TriageStatus");
        }
    }
}
