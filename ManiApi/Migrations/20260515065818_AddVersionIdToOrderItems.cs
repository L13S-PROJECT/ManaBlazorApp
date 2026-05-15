using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManiApi.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionIdToOrderItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_StockMovements",
                table: "StockMovements");

            migrationBuilder.RenameTable(
                name: "StockMovements",
                newName: "stock_movements");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "stock_movements",
                newName: "ID");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "workcentr_type",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Step_Type_ID",
                table: "workcentr_type",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WorkCenter_Order",
                table: "workcentr_type",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "is_priority",
                table: "versions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Estimated_Minutes",
                table: "toppartsteps",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPainting",
                table: "toppartsteps",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<byte>(
                name: "Stage",
                table: "toppart",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<byte>(
                name: "TopPartType",
                table: "toppart",
                type: "tinyint unsigned",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AlterColumn<int>(
                name: "Task_ID",
                table: "stock_movements",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "BatchProduct_ID",
                table: "stock_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RAL_Color_ID",
                table: "stock_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceBatchProduct_ID",
                table: "stock_movements",
                type: "int",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_stock_movements",
                table: "stock_movements",
                column: "ID");

            migrationBuilder.CreateTable(
                name: "batches",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Batches_Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Batches_Statuss = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "batches_products",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Batch_Id = table.Column<int>(type: "int", nullable: false),
                    Version_Id = table.Column<int>(type: "int", nullable: false),
                    Planned_Qty = table.Column<int>(type: "int", nullable: false),
                    Done_Qty = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_priority = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    NormalOrder = table.Column<int>(type: "int", nullable: false),
                    ProductToPart_ID = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batches_products", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BatchProductLinks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ParentBatchProduct_ID = table.Column<int>(type: "int", nullable: false),
                    ChildBatchProduct_ID = table.Column<int>(type: "int", nullable: false),
                    Qty_Required = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchProductLinks", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "BatchProductMaterials",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BatchProduct_ID = table.Column<int>(type: "int", nullable: false),
                    SourceBatchProduct_ID = table.Column<int>(type: "int", nullable: false),
                    Qty = table.Column<int>(type: "int", nullable: false),
                    Created_At = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Task_ID = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchProductMaterials", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "company_calendar",
                columns: table => new
                {
                    WorkDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WorkStart = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    WorkEnd = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    BreakMinutes = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UseEmployeeDefaults = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_calendar", x => x.WorkDate);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "company_calendar_breaks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    WorkDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BreakStart = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    BreakEnd = table.Column<TimeSpan>(type: "time(6)", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_company_calendar_breaks", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "customer_code_map",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    customer_name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    customer_code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    version_id = table.Column<int>(type: "int", nullable: true),
                    producttopart_id = table.Column<int>(type: "int", nullable: true),
                    top_part_id = table.Column<int>(type: "int", nullable: true),
                    ral_color_id = table.Column<int>(type: "int", nullable: true),
                    is_product = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_part = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_code_map", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "employee_availability",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    DateFrom = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DateTo = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Hours = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_availability", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "employee_work_log",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    EmployeeID = table.Column<int>(type: "int", nullable: false),
                    WorkDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TimeFrom = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    TimeTo = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    Hours = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    BreaksJson = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Notes = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BreakMinutes = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_work_log", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Employee_Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UserName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Password = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Role = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WorkCentrTypeID = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultDailyHours = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    WorkStart = table.Column<TimeSpan>(type: "time(6)", nullable: true),
                    WorkEnd = table.Column<TimeSpan>(type: "time(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "order_draft_items",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Order_Draft_ID = table.Column<int>(type: "int", nullable: false),
                    Customer_Code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Version_ID = table.Column<int>(type: "int", nullable: true),
                    producttopart_id = table.Column<int>(type: "int", nullable: true),
                    Ral_Color_ID = table.Column<int>(type: "int", nullable: true),
                    Is_Mapped = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Is_Active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    top_part_id = table.Column<int>(type: "int", nullable: true),
                    is_product = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_part = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    customer_code_map_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_draft_items", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "order_drafts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Order_Number = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order_Date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Customer_Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Created_At = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_drafts", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    order_id = table.Column<int>(type: "int", nullable: false),
                    customer_code = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    customer_code_map_id = table.Column<int>(type: "int", nullable: true),
                    VersionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_order_items", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    order_number = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    order_date = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    customer_name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    comment = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_orders", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ral_colors",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ral_colors", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stage_step_type_map",
                columns: table => new
                {
                    Stage = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    Step_Type_ID = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stage_step_type_map", x => x.Stage);
                    table.ForeignKey(
                        name: "FK_stage_step_type_map_step_type_Step_Type_ID",
                        column: x => x.Step_Type_ID,
                        principalTable: "step_type",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    BatchProduct_ID = table.Column<int>(type: "int", nullable: false),
                    TopPartStep_ID = table.Column<int>(type: "int", nullable: false),
                    Tasks_Priority = table.Column<int>(type: "int", nullable: false),
                    Tasks_Push = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Qty_Done = table.Column<int>(type: "int", nullable: false),
                    Qty_Scrap = table.Column<int>(type: "int", nullable: false),
                    Tasks_Status = table.Column<int>(type: "int", nullable: false),
                    RAL_Color_ID = table.Column<int>(type: "int", nullable: true),
                    Tasks_Comment = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Is_Comment_For_Employee = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Started_At = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Finished_At = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Assigned_To = table.Column<int>(type: "int", nullable: true),
                    Claimed_By = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tasks_work_sessions",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Task_ID = table.Column<int>(type: "int", nullable: false),
                    Employee_ID = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DurationMinutes = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks_work_sessions", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_stage_step_type_map_Step_Type_ID",
                table: "stage_step_type_map",
                column: "Step_Type_ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batches");

            migrationBuilder.DropTable(
                name: "batches_products");

            migrationBuilder.DropTable(
                name: "BatchProductLinks");

            migrationBuilder.DropTable(
                name: "BatchProductMaterials");

            migrationBuilder.DropTable(
                name: "company_calendar");

            migrationBuilder.DropTable(
                name: "company_calendar_breaks");

            migrationBuilder.DropTable(
                name: "customer_code_map");

            migrationBuilder.DropTable(
                name: "employee_availability");

            migrationBuilder.DropTable(
                name: "employee_work_log");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "order_draft_items");

            migrationBuilder.DropTable(
                name: "order_drafts");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "orders");

            migrationBuilder.DropTable(
                name: "ral_colors");

            migrationBuilder.DropTable(
                name: "stage_step_type_map");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "tasks_work_sessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_stock_movements",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "workcentr_type");

            migrationBuilder.DropColumn(
                name: "Step_Type_ID",
                table: "workcentr_type");

            migrationBuilder.DropColumn(
                name: "WorkCenter_Order",
                table: "workcentr_type");

            migrationBuilder.DropColumn(
                name: "is_priority",
                table: "versions");

            migrationBuilder.DropColumn(
                name: "Estimated_Minutes",
                table: "toppartsteps");

            migrationBuilder.DropColumn(
                name: "IsPainting",
                table: "toppartsteps");

            migrationBuilder.DropColumn(
                name: "Stage",
                table: "toppart");

            migrationBuilder.DropColumn(
                name: "TopPartType",
                table: "toppart");

            migrationBuilder.DropColumn(
                name: "BatchProduct_ID",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "RAL_Color_ID",
                table: "stock_movements");

            migrationBuilder.DropColumn(
                name: "SourceBatchProduct_ID",
                table: "stock_movements");

            migrationBuilder.RenameTable(
                name: "stock_movements",
                newName: "StockMovements");

            migrationBuilder.RenameColumn(
                name: "ID",
                table: "StockMovements",
                newName: "Id");

            migrationBuilder.AlterColumn<int>(
                name: "Task_ID",
                table: "StockMovements",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_StockMovements",
                table: "StockMovements",
                column: "Id");
        }
    }
}
