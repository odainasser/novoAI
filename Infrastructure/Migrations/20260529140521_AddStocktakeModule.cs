using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakeModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StocktakeId",
                table: "StockAdjustments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StocktakeNumber",
                table: "StockAdjustments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Stocktakes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StocktakeNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScopeType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ScopeCategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ApprovedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_Stocktakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stocktakes_Categories_ScopeCategoryId",
                        column: x => x.ScopeCategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Stocktakes_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StocktakeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StocktakeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemQuantity = table.Column<int>(type: "int", nullable: false),
                    CountedQuantity = table.Column<int>(type: "int", nullable: true),
                    Difference = table.Column<int>(type: "int", nullable: false),
                    LineStatus = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    GeneratedAdjustmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GeneratedAdjustmentNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_StocktakeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StocktakeLines_SellingUnits_UnitId",
                        column: x => x.UnitId,
                        principalTable: "SellingUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StocktakeLines_Stocktakes_StocktakeId",
                        column: x => x.StocktakeId,
                        principalTable: "Stocktakes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockAdjustments_StocktakeId",
                table: "StockAdjustments",
                column: "StocktakeId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLines_StocktakeId",
                table: "StocktakeLines",
                column: "StocktakeId");

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeLines_UnitId",
                table: "StocktakeLines",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_CreatedAt",
                table: "Stocktakes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_ScopeCategoryId",
                table: "Stocktakes",
                column: "ScopeCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_Status",
                table: "Stocktakes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_StocktakeNumber",
                table: "Stocktakes",
                column: "StocktakeNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_Type",
                table: "Stocktakes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Stocktakes_WarehouseId",
                table: "Stocktakes",
                column: "WarehouseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StocktakeLines");

            migrationBuilder.DropTable(
                name: "Stocktakes");

            migrationBuilder.DropIndex(
                name: "IX_StockAdjustments_StocktakeId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "StocktakeId",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "StocktakeNumber",
                table: "StockAdjustments");
        }
    }
}
