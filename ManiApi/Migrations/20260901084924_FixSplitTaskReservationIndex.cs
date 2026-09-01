using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManiApi.Migrations
{
    /// <inheritdoc />
    public partial class FixSplitTaskReservationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_new_reservation",
                table: "stock_movements_new");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_new_ProductionReservation_ID",
                table: "stock_movements_new",
                column: "ProductionReservation_ID");

            migrationBuilder.DropIndex(
                name: "UX_stock_movements_new_reservation",
                table: "stock_movements_new");

            migrationBuilder.CreateIndex(
                name: "UX_stock_movements_new_task_reservation",
                table: "stock_movements_new",
                columns: new[] { "TaskNew_ID", "ProductionReservation_ID" },
                unique: true);
            
            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_new_reservation",
                table: "stock_movements_new",
                column: "ProductionReservation_ID",
                principalTable: "production_reservations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_new_reservation",
                table: "stock_movements_new");

            migrationBuilder.CreateIndex(
                name: "UX_stock_movements_new_reservation",
                table: "stock_movements_new",
                column: "ProductionReservation_ID",
                unique: true);
            
            migrationBuilder.DropIndex(
                name: "IX_stock_movements_new_ProductionReservation_ID",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "UX_stock_movements_new_task_reservation",
                table: "stock_movements_new");
            
            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_new_reservation",
                table: "stock_movements_new",
                column: "ProductionReservation_ID",
                principalTable: "production_reservations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

        }
    }
}
