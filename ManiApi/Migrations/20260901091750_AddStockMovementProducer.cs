using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManiApi.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovementProducer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "ProducedByTaskNew_ID",
                table: "stock_movements_new",
                type: "int unsigned",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_stock_movements_new_producer_output",
                table: "stock_movements_new",
                columns: new[] { "ProducedByTaskNew_ID", "WorkflowNode_ID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_new_tasks_new_ProducedByTaskNew_ID",
                table: "stock_movements_new",
                column: "ProducedByTaskNew_ID",
                principalTable: "tasks_new",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_new_tasks_new_ProducedByTaskNew_ID",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "UX_stock_movements_new_producer_output",
                table: "stock_movements_new");

            migrationBuilder.DropColumn(
                name: "ProducedByTaskNew_ID",
                table: "stock_movements_new");
        }
    }
}
