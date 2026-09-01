using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManiApi.Migrations
{
    /// <inheritdoc />
    public partial class CompleteStockReservationCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkflowProcessComponent_ID",
                table: "production_requirements",
                type: "int",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_production_requirements_process_component",
                table: "production_requirements",
                column: "WorkflowProcessComponent_ID");

            migrationBuilder.AddForeignKey(
                name: "FK_production_requirements_workflowprocesscomponents",
                table: "production_requirements",
                column: "WorkflowProcessComponent_ID",
                principalTable: "workflowprocesscomponents",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
            
            migrationBuilder.AddColumn<uint>(
                name: "ProductionReservation_ID",
                table: "stock_movements_new",
                type: "int unsigned",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "ReversalOfMovement_ID",
                table: "stock_movements_new",
                type: "int unsigned",
                nullable: true);
            
            migrationBuilder.DropForeignKey(
                name: "FK_stock_new_source_movement",
                table: "stock_movements_new");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_new_task",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "UX_stock_source_movement",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "UX_stock_movements_new_task",
                table: "stock_movements_new");

            migrationBuilder.CreateIndex(
                name: "IX_stock_source_movement",
                table: "stock_movements_new",
                column: "SourceMovement_ID");

            migrationBuilder.CreateIndex(
                name: "IX_stock_movements_new_task",
                table: "stock_movements_new",
                column: "TaskNew_ID");

            migrationBuilder.CreateIndex(
                name: "UX_stock_movements_new_reservation",
                table: "stock_movements_new",
                column: "ProductionReservation_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_stock_reversal_movement",
                table: "stock_movements_new",
                column: "ReversalOfMovement_ID",
                unique: true);
            
            migrationBuilder.AddForeignKey(
                name: "FK_stock_new_source_movement",
                table: "stock_movements_new",
                column: "SourceMovement_ID",
                principalTable: "stock_movements_new",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_new_task",
                table: "stock_movements_new",
                column: "TaskNew_ID",
                principalTable: "tasks_new",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_new_reservation",
                table: "stock_movements_new",
                column: "ProductionReservation_ID",
                principalTable: "production_reservations",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_new_reversal",
                table: "stock_movements_new",
                column: "ReversalOfMovement_ID",
                principalTable: "stock_movements_new",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_new_reservation",
                table: "stock_movements_new");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_new_reversal",
                table: "stock_movements_new");

            migrationBuilder.DropForeignKey(
                name: "FK_production_requirements_workflowprocesscomponents",
                table: "production_requirements");

            migrationBuilder.DropIndex(
                name: "UX_stock_movements_new_reservation",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "UX_stock_reversal_movement",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "IX_production_requirements_process_component",
                table: "production_requirements");

            migrationBuilder.DropColumn(
                name: "ProductionReservation_ID",
                table: "stock_movements_new");

            migrationBuilder.DropColumn(
                name: "ReversalOfMovement_ID",
                table: "stock_movements_new");

            migrationBuilder.DropColumn(
                name: "WorkflowProcessComponent_ID",
                table: "production_requirements");
            
            migrationBuilder.DropForeignKey(
                name: "FK_stock_new_source_movement",
                table: "stock_movements_new");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_new_task",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "IX_stock_source_movement",
                table: "stock_movements_new");

            migrationBuilder.DropIndex(
                name: "IX_stock_movements_new_task",
                table: "stock_movements_new");

            migrationBuilder.CreateIndex(
                name: "UX_stock_source_movement",
                table: "stock_movements_new",
                column: "SourceMovement_ID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_stock_movements_new_task",
                table: "stock_movements_new",
                column: "TaskNew_ID",
                unique: true);
            
            migrationBuilder.AddForeignKey(
                name: "FK_stock_new_source_movement",
                table: "stock_movements_new",
                column: "SourceMovement_ID",
                principalTable: "stock_movements_new",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_new_task",
                table: "stock_movements_new",
                column: "TaskNew_ID",
                principalTable: "tasks_new",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

        }
    }
}
