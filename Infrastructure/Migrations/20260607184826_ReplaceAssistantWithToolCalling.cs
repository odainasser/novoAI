using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAssistantWithToolCalling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeywordMisses");

            migrationBuilder.DropTable(
                name: "KeywordSuggestions");

            migrationBuilder.DropTable(
                name: "KeywordTriggers");

            migrationBuilder.CreateTable(
                name: "AssistantInteractions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ToolsUsed = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Answered = table.Column<bool>(type: "bit", nullable: false),
                    IsMixing = table.Column<bool>(type: "bit", nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantInteractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssistantToolCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormalizedQuestion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    SampleQuestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    LikelyMixing = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_AssistantToolCandidates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractions_CreatedAt",
                table: "AssistantInteractions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractions_IsMixing",
                table: "AssistantInteractions",
                column: "IsMixing");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantToolCandidates_NormalizedQuestion",
                table: "AssistantToolCandidates",
                column: "NormalizedQuestion",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantToolCandidates_Status",
                table: "AssistantToolCandidates",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantInteractions");

            migrationBuilder.DropTable(
                name: "AssistantToolCandidates");

            migrationBuilder.CreateTable(
                name: "KeywordMisses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExtractedAction = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtractedEntity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtractedPeriod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtractedStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Resolved = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordMisses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeywordSuggestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dimension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SampleQuestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SlotValue = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Word = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeywordTriggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dimension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    SlotValue = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Word = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeywordTriggers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeywordMisses_CreatedAt",
                table: "KeywordMisses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordSuggestions_Dimension_SlotValue_Word",
                table: "KeywordSuggestions",
                columns: new[] { "Dimension", "SlotValue", "Word" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeywordSuggestions_Status",
                table: "KeywordSuggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTriggers_Dimension_SlotValue_Word",
                table: "KeywordTriggers",
                columns: new[] { "Dimension", "SlotValue", "Word" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KeywordTriggers_IsActive",
                table: "KeywordTriggers",
                column: "IsActive");
        }
    }
}
