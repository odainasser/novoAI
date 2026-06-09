using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssistantPlanReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssistantToolCandidates");

            migrationBuilder.DropIndex(
                name: "IX_AssistantInteractions_IsMixing",
                table: "AssistantInteractions");

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedDomain",
                table: "AssistantInteractions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedEntities",
                table: "AssistantInteractions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedTools",
                table: "AssistantInteractions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PlanConfirmed",
                table: "AssistantInteractions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "AssistantInteractions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "AssistantInteractions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssistantInteractions_PlanConfirmed",
                table: "AssistantInteractions",
                column: "PlanConfirmed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssistantInteractions_PlanConfirmed",
                table: "AssistantInteractions");

            migrationBuilder.DropColumn(
                name: "ConfirmedDomain",
                table: "AssistantInteractions");

            migrationBuilder.DropColumn(
                name: "ConfirmedEntities",
                table: "AssistantInteractions");

            migrationBuilder.DropColumn(
                name: "ConfirmedTools",
                table: "AssistantInteractions");

            migrationBuilder.DropColumn(
                name: "PlanConfirmed",
                table: "AssistantInteractions");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "AssistantInteractions");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "AssistantInteractions");

            migrationBuilder.CreateTable(
                name: "AssistantToolCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Frequency = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    LikelyMixing = table.Column<bool>(type: "bit", nullable: false),
                    Locale = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    NormalizedQuestion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SampleQuestion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssistantToolCandidates", x => x.Id);
                });

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
    }
}
