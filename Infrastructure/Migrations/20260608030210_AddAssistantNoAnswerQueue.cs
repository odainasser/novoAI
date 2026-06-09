using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantNoAnswerQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssistantNoAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    ReviewedReason = table.Column<int>(type: "int", nullable: true),
                    NormalizedQuestion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClusterKey = table.Column<string>(type: "nvarchar(420)", maxLength: 420, nullable: false),
                    SampleQuestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    UserFacingMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SampleInteractionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriageStatus = table.Column<int>(type: "int", nullable: false),
                    TriageNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
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
                    table.PrimaryKey("PK_AssistantNoAnswers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoAnswers_ClusterKey",
                table: "AssistantNoAnswers",
                column: "ClusterKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoAnswers_Frequency",
                table: "AssistantNoAnswers",
                column: "Frequency");

            migrationBuilder.CreateIndex(
                name: "IX_AssistantNoAnswers_TriageStatus",
                table: "AssistantNoAnswers",
                column: "TriageStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantNoAnswers");
        }
    }
}
