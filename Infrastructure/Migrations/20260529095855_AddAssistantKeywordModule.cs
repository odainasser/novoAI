using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantKeywordModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KeywordMisses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExtractedAction = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtractedEntity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtractedPeriod = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ExtractedStatus = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Resolved = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    Dimension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SlotValue = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Word = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    SampleQuestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_KeywordSuggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KeywordTriggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Dimension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SlotValue = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Word = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeywordMisses");

            migrationBuilder.DropTable(
                name: "KeywordSuggestions");

            migrationBuilder.DropTable(
                name: "KeywordTriggers");
        }
    }
}
