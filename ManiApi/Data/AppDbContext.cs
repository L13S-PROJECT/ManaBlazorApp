using Microsoft.EntityFrameworkCore;
using ManiApi.Models;


namespace ManiApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductVersion> ProductVersions { get; set; }
        public DbSet<TopPart> TopParts { get; set; }
        public DbSet<TopPartCategory> TopPartCategories { get; set; }
        public DbSet<ProductTopPart> ProductTopParts { get; set; }
        public DbSet<TopPartStep> TopPartSteps { get; set; }
        public DbSet<RalColor> RalColors { get; set; }
        public DbSet<ManiApi.Models.StepType> StepTypes { get; set; }
        public DbSet<ManiApi.Models.Employee> Employees { get; set; }
        public DbSet<ManiApi.Models.EmployeeAvailability> EmployeeAvailabilities { get; set; }
        public DbSet<ManiApi.Models.EmployeeWorkLog> EmployeeWorkLogs { get; set; }
        public DbSet<CompanyCalendar> CompanyCalendars { get; set; }
        public DbSet<ManiApi.Models.Tasks> Tasks { get; set; }
        public DbSet<TaskWorkSession> TaskWorkSessions { get; set; }
        public DbSet<ManiApi.Models.WorkCenter> WorkCentrs { get; set; }
        public DbSet<ManiApi.Models.StockMovement> StockMovements { get; set; }
        public DbSet<StageStepTypeMap> StageStepTypeMaps { get; set; }
        public DbSet<CompanyCalendarBreak> CompanyCalendarBreaks { get; set; }
        public DbSet<WorkCenter> WorkCenters { get; set; }
        public DbSet<BatchProductLink> BatchProductLinks { get; set; }
        public DbSet<BatchProductMaterial> BatchProductMaterials { get; set; }
        public DbSet<BatchProduct> BatchProducts { get; set; }
        public DbSet<Batch> Batches { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<CustomerCodeMap> CustomerCodeMaps { get; set; }
        public DbSet<OrderDraft> OrderDrafts { get; set; }
        public DbSet<OrderDraftItem> OrderDraftItems { get; set; }
        public DbSet<Workflow> Workflows { get; set; }
        public DbSet<WorkflowNode> WorkflowNodes { get; set; }
        public DbSet<WorkflowNodeConnection> WorkflowNodeConnections { get; set; }
        public DbSet<WorkflowDependency> WorkflowDependencies { get; set; }

        public DbSet<ItemType> ItemTypes { get; set; }
        public DbSet<Item> Items { get; set; }
        public DbSet<Unit> Units => Set<Unit>();
        public DbSet<WorkflowComponent> WorkflowComponents { get; set; }
        public DbSet<WorkflowProcessComponent> WorkflowProcessComponents { get; set; }

        public DbSet<ProductTopPartItem> ProductTopPartItems { get; set; }

        public DbSet<ProductionBatch> ProductionBatches { get; set; }
        public DbSet<ProductionBatchTopPart> ProductionBatchTopParts { get; set; }
        public DbSet<StockMovementNew> StockMovementsNew { get; set; }
        public DbSet<TopPartSparePart> TopPartSpareParts { get; set; }
        public DbSet<ProductionPlanningDraft> ProductionPlanningDrafts { get; set; }
        public DbSet<ProductionPlanningDraftItem> ProductionPlanningDraftItems { get; set; }
        public DbSet<ProductionExecution> ProductionExecutions { get; set; }
        public DbSet<ProductionRequirement> ProductionRequirements { get; set; }
        public DbSet<ProductionReservation> ProductionReservations { get; set; }
        public DbSet<TaskNew> TasksNew { get; set; }
        public DbSet<TaskNewStatusHistory> TaskNewStatusHistories { get; set; }
        public DbSet<TaskNewDependency> TaskNewDependencies { get; set; }

        public DbSet<ProductionComponentStaging> ProductionComponentStagings { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
        
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.Entity<ProductionBatch>()
        .ToTable("production_batches");

    modelBuilder.Entity<ProductionBatchTopPart>()
        .ToTable("production_batch_topparts");

    modelBuilder.Entity<StockMovementNew>()
        .ToTable("stock_movements_new");
    
    modelBuilder.Entity<StockMovementNew>()
        .Property(x => x.Movement_Type)
        .HasConversion<string>();

    modelBuilder.Entity<StockMovementNew>()
        .HasIndex(x => x.SourceMovement_ID)
        .HasDatabaseName("IX_stock_source_movement");

    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.ReversalOfMovement)
        .WithMany()
        .HasForeignKey(x => x.ReversalOfMovement_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<StockMovementNew>()
        .HasIndex(x => x.ReversalOfMovement_ID)
        .IsUnique()
        .HasDatabaseName("UX_stock_reversal_movement");

    modelBuilder.Entity<ProductionBatch>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionBatch>()
        .HasIndex(x => x.Batch_Code)
        .IsUnique()
        .HasDatabaseName("UX_production_batches_code");

    modelBuilder.Entity<ProductionBatchTopPart>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<StockMovementNew>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionBatchTopPart>()
    .HasOne(x => x.Batch)
    .WithMany()
    .HasForeignKey(x => x.Batch_ID)
    .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionBatchTopPart>()
        .HasOne(x => x.TopPart)
        .WithMany()
        .HasForeignKey(x => x.TopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionBatchTopPart>()
        .HasOne(x => x.Workflow)
        .WithMany()
        .HasForeignKey(x => x.Workflow_ID)
        .OnDelete(DeleteBehavior.Restrict);
    
    modelBuilder.Entity<StockMovementNew>()
    .HasOne(x => x.TopPart)
    .WithMany()
    .HasForeignKey(x => x.TopPart_ID)
    .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.ProductionBatchTopPart)
        .WithMany()
        .HasForeignKey(x => x.ProductionBatchTopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.RalColor)
        .WithMany()
        .HasForeignKey(x => x.RAL_Color_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.SourceMovement)
        .WithMany()
        .HasForeignKey(x => x.SourceMovement_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.ConsumedByBatch)
        .WithMany()
        .HasForeignKey(x => x.ConsumedByBatch_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.WorkflowNode)
        .WithMany()
        .HasForeignKey(x => x.WorkflowNode_ID)
        .OnDelete(DeleteBehavior.Restrict);
    
    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.TaskNew)
        .WithMany()
        .HasForeignKey(x => x.TaskNew_ID)
        .OnDelete(DeleteBehavior.Restrict);
    
    modelBuilder.Entity<StockMovementNew>()
        .HasIndex(x => x.TaskNew_ID)
        .HasDatabaseName("IX_stock_movements_new_task");

    modelBuilder.Entity<StockMovementNew>()
        .HasOne(x => x.ProductionReservation)
        .WithMany()
        .HasForeignKey(x => x.ProductionReservation_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<StockMovementNew>()
        .HasIndex(x => x.ProductionReservation_ID)
        .IsUnique()
        .HasDatabaseName("UX_stock_movements_new_reservation");


    modelBuilder.Entity<ProductionPlanningDraft>()
        .ToTable("production_planning_drafts");

    modelBuilder.Entity<ProductionPlanningDraft>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionPlanningDraftItem>()
        .ToTable("production_planning_draft_items");

    modelBuilder.Entity<ProductionPlanningDraftItem>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionPlanningDraftItem>()
        .HasOne(x => x.Draft)
        .WithMany()
        .HasForeignKey(x => x.Draft_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionPlanningDraftItem>()
        .HasOne(x => x.TopPart)
        .WithMany()
        .HasForeignKey(x => x.TopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionPlanningDraftItem>()
        .HasOne(x => x.Workflow)
        .WithMany()
        .HasForeignKey(x => x.Workflow_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionPlanningDraft>()
        .HasOne(x => x.SourceBatch)
        .WithMany()
        .HasForeignKey(x => x.Source_Batch_ID)
        .OnDelete(DeleteBehavior.Restrict);
    
    modelBuilder.Entity<ProductionExecution>()
    .ToTable("production_executions");

    modelBuilder.Entity<ProductionExecution>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionExecution>()
        .Property(x => x.Status)
        .HasConversion<string>();

    modelBuilder.Entity<ProductionExecution>()
    .HasOne(x => x.ProductionBatchTopPart)
    .WithMany()
    .HasForeignKey(x => x.ProductionBatchTopPart_ID)
    .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionExecution>()
        .HasOne(x => x.TopPart)
        .WithMany()
        .HasForeignKey(x => x.TopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionExecution>()
        .HasOne(x => x.Workflow)
        .WithMany()
        .HasForeignKey(x => x.Workflow_ID)
        .OnDelete(DeleteBehavior.Restrict);
    
    modelBuilder.Entity<ProductionExecution>()
        .HasIndex(x => x.ProductionBatchTopPart_ID)
        .HasDatabaseName("IX_production_executions_batch_toppart");

    modelBuilder.Entity<ProductionExecution>()
        .HasIndex(x => x.TopPart_ID)
        .HasDatabaseName("IX_production_executions_top_part");

    modelBuilder.Entity<ProductionExecution>()
        .HasIndex(x => x.Workflow_ID)
        .HasDatabaseName("IX_production_executions_workflow");

    modelBuilder.Entity<ProductionExecution>()
        .HasIndex(x => new { x.Status, x.IsActive })
        .HasDatabaseName("IX_production_executions_status");

    modelBuilder.Entity<ProductionExecution>()
        .HasOne(x => x.ProductionRequirement)
        .WithMany()
        .HasForeignKey(x => x.ProductionRequirement_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionExecution>()
        .HasIndex(x => x.ProductionRequirement_ID)
        .HasDatabaseName("IX_production_executions_requirement");

    modelBuilder.Entity<ProductionRequirement>()
        .ToTable("production_requirements");

    modelBuilder.Entity<ProductionRequirement>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionRequirement>()
        .Property(x => x.SourceType)
        .HasConversion<string>();

    modelBuilder.Entity<ProductionRequirement>()
        .HasOne(x => x.ProductionPlanningDraftItem)
        .WithMany()
        .HasForeignKey(x => x.ProductionPlanningDraftItem_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionRequirement>()
        .HasOne(x => x.ProductionBatchTopPart)
        .WithMany()
        .HasForeignKey(x => x.ProductionBatchTopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionRequirement>()
        .HasOne(x => x.SourceTopPart)
        .WithMany()
        .HasForeignKey(x => x.SourceTopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionRequirement>()
        .HasOne(x => x.RequiredTopPart)
        .WithMany()
        .HasForeignKey(x => x.RequiredTopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionRequirement>()
        .HasOne(x => x.ParentRequirement)
        .WithMany(x => x.ChildRequirements)
        .HasForeignKey(x => x.ParentRequirement_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionRequirement>()
        .HasIndex(x => x.ProductionPlanningDraftItem_ID)
        .HasDatabaseName("IX_production_requirements_draft_item");

    modelBuilder.Entity<ProductionRequirement>()
        .HasIndex(x => x.ProductionBatchTopPart_ID)
        .HasDatabaseName("IX_production_requirements_batch_toppart");

    modelBuilder.Entity<ProductionRequirement>()
        .HasIndex(x => new { x.RequiredTopPart_ID, x.IsActive })
        .HasDatabaseName("IX_production_requirements_required");

    modelBuilder.Entity<ProductionRequirement>()
        .HasIndex(x => new { x.Priority, x.Created_At })
        .HasDatabaseName("IX_production_requirements_priority");

    modelBuilder.Entity<ProductionRequirement>()
        .HasIndex(x => x.ParentRequirement_ID)
        .HasDatabaseName("IX_production_requirements_parent");
    
    modelBuilder.Entity<ProductionRequirement>()
        .HasOne(x => x.WorkflowProcessComponent)
        .WithMany()
        .HasForeignKey(x => x.WorkflowProcessComponent_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionRequirement>()
        .HasIndex(x => x.WorkflowProcessComponent_ID)
        .HasDatabaseName("IX_production_requirements_process_component");
    
    modelBuilder.Entity<ProductionRequirement>()
        .HasIndex(x => x.SourceTopPart_ID)
        .HasDatabaseName("IX_production_requirements_source");

    modelBuilder.Entity<ProductionReservation>()
        .ToTable("production_reservations");

    modelBuilder.Entity<ProductionReservation>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionReservation>()
        .Property(x => x.Status)
        .HasConversion<string>();

    modelBuilder.Entity<ProductionReservation>()
        .Ignore(x => x.RemainingQuantity);
    
    modelBuilder.Entity<ProductionReservation>()
        .HasOne(x => x.ProductionRequirement)
        .WithMany()
        .HasForeignKey(x => x.ProductionRequirement_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionReservation>()
        .HasOne(x => x.TopPart)
        .WithMany()
        .HasForeignKey(x => x.TopPart_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionReservation>()
        .HasOne(x => x.SourceMovement)
        .WithMany()
        .HasForeignKey(x => x.SourceMovement_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionReservation>()
        .HasOne(x => x.SourceWorkflow)
        .WithMany()
        .HasForeignKey(x => x.SourceWorkflow_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionReservation>()
        .HasOne(x => x.SourceWorkflowNode)
        .WithMany()
        .HasForeignKey(x => x.SourceWorkflowNode_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionReservation>()
        .HasIndex(x => x.ProductionRequirement_ID)
        .HasDatabaseName("IX_production_reservations_requirement");

    modelBuilder.Entity<ProductionReservation>()
        .HasIndex(x => new { x.TopPart_ID, x.Status, x.IsActive })
        .HasDatabaseName("IX_production_reservations_top_part");

    modelBuilder.Entity<ProductionReservation>()
        .HasIndex(x => new { x.SourceMovement_ID, x.Status, x.IsActive })
        .HasDatabaseName("IX_production_reservations_source");

    modelBuilder.Entity<ProductionReservation>()
        .HasIndex(x => new { x.SourceWorkflow_ID, x.SourceWorkflowNode_ID })
        .HasDatabaseName("IX_production_reservations_workflow_node");
    
    modelBuilder.Entity<TaskNew>()
    .ToTable("tasks_new");

    modelBuilder.Entity<TaskNew>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<TaskNew>()
        .Property(x => x.Status)
        .HasConversion<string>();
    
    modelBuilder.Entity<TaskNew>()
    .HasOne(x => x.ProductionExecution)
    .WithMany()
    .HasForeignKey(x => x.ProductionExecution_ID)
    .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<TaskNew>()
        .HasOne(x => x.WorkflowNode)
        .WithMany()
        .HasForeignKey(x => x.WorkflowNode_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<TaskNew>()
        .HasOne(x => x.Employee)
        .WithMany()
        .HasForeignKey(x => x.Employee_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<TaskNew>()
        .HasOne(x => x.WorkCenter)
        .WithMany()
        .HasForeignKey(x => x.WorkCenter_ID)
        .OnDelete(DeleteBehavior.Restrict);
    
    modelBuilder.Entity<TaskNew>()
        .HasIndex(x => new
        {
            x.ProductionExecution_ID,
            x.WorkflowNode_ID
        })
        .HasDatabaseName("IX_tasks_new_execution_node");

    modelBuilder.Entity<TaskNew>()
        .HasIndex(x => new { x.Employee_ID, x.Status, x.IsActive })
        .HasDatabaseName("IX_tasks_new_employee_status");

    modelBuilder.Entity<TaskNew>()
        .HasIndex(x => new { x.WorkCenter_ID, x.Status, x.IsActive })
        .HasDatabaseName("IX_tasks_new_workcenter_status");
    
    modelBuilder.Entity<TaskNewStatusHistory>()
        .ToTable("tasks_new_status_history");

    modelBuilder.Entity<TaskNewStatusHistory>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<TaskNewStatusHistory>()
        .Property(x => x.FromStatus)
        .HasConversion<string>();

    modelBuilder.Entity<TaskNewStatusHistory>()
        .Property(x => x.ToStatus)
        .HasConversion<string>();

    modelBuilder.Entity<TaskNewStatusHistory>()
        .Property(x => x.Comment)
        .HasMaxLength(500);
    
    modelBuilder.Entity<TaskNewStatusHistory>()
        .HasOne(x => x.TaskNew)
        .WithMany()
        .HasForeignKey(x => x.TaskNew_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<TaskNewStatusHistory>()
        .HasOne(x => x.ChangedByEmployee)
        .WithMany()
        .HasForeignKey(x => x.ChangedByEmployee_ID)
        .OnDelete(DeleteBehavior.Restrict);
    
    modelBuilder.Entity<TaskNewStatusHistory>()
        .HasIndex(x => new { x.TaskNew_ID, x.Changed_At })
        .HasDatabaseName("IX_tasks_new_status_history_task");

    modelBuilder.Entity<TaskNewStatusHistory>()
        .HasIndex(x => x.ChangedByEmployee_ID)
        .HasDatabaseName("IX_tasks_new_status_history_employee");
    
    modelBuilder.Entity<TaskNewDependency>()
        .ToTable("tasks_new_dependencies");

    modelBuilder.Entity<TaskNewDependency>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<TaskNewDependency>()
        .HasOne(x => x.TaskNew)
        .WithMany()
        .HasForeignKey(x => x.TaskNew_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<TaskNewDependency>()
        .HasOne(x => x.DependsOnTaskNew)
        .WithMany()
        .HasForeignKey(x => x.DependsOnTaskNew_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<TaskNewDependency>()
        .HasIndex(x => new
        {
            x.TaskNew_ID,
            x.DependsOnTaskNew_ID
        })
        .IsUnique()
        .HasDatabaseName("UX_tasks_new_dependencies_pair");

    modelBuilder.Entity<TaskNewDependency>()
        .HasIndex(x => x.DependsOnTaskNew_ID)
        .HasDatabaseName("IX_tasks_new_dependencies_depends_on");

    modelBuilder.Entity<ProductionComponentStaging>()
        .ToTable("production_component_staging");

    modelBuilder.Entity<ProductionComponentStaging>()
        .HasKey(x => x.ID);

    modelBuilder.Entity<ProductionComponentStaging>()
        .Property(x => x.RequiredQuantity)
        .HasPrecision(18, 3);

    modelBuilder.Entity<ProductionComponentStaging>()
        .Property(x => x.StagedQuantity)
        .HasPrecision(18, 3);

    modelBuilder.Entity<ProductionComponentStaging>()
        .HasOne(x => x.ProductionExecution)
        .WithMany()
        .HasForeignKey(x => x.ProductionExecution_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionComponentStaging>()
        .HasOne(x => x.WorkflowProcessComponent)
        .WithMany()
        .HasForeignKey(x => x.WorkflowProcessComponent_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionComponentStaging>()
        .HasOne(x => x.StagedByEmployee)
        .WithMany()
        .HasForeignKey(x => x.StagedByEmployee_ID)
        .OnDelete(DeleteBehavior.Restrict);

    modelBuilder.Entity<ProductionComponentStaging>()
        .HasIndex(x => new
        {
            x.ProductionExecution_ID,
            x.WorkflowProcessComponent_ID
        })
        .IsUnique()
        .HasDatabaseName("UX_component_staging_execution_process_component");


    modelBuilder.Entity<EmployeeWorkLog>().ToTable("employee_work_log");
    modelBuilder.Entity<EmployeeAvailability>().ToTable("employee_availability");
    // <- PIESPIED tabulas nosaukumu ar underscore
    modelBuilder.Entity<StockMovement>().ToTable("stock_movements");
    modelBuilder.Entity<Order>().ToTable("orders");
    modelBuilder.Entity<OrderItem>().ToTable("order_items");
    modelBuilder.Entity<CustomerCodeMap>().ToTable("customer_code_map");
    modelBuilder.Entity<StockMovement>()
        .Property(sm => sm.Move_Type)
        .HasConversion<string>();
    modelBuilder.Entity<StageStepTypeMap>(entity =>
{
    entity.ToTable("stage_step_type_map");

    entity.HasKey(e => e.Stage);

    entity.Property(e => e.Step_Type_ID)
        .HasColumnName("Step_Type_ID");

    entity.Property(e => e.IsActive)
        .HasColumnName("IsActive");

    entity.HasOne(e => e.StepType)
        .WithMany()
        .HasForeignKey(e => e.Step_Type_ID)
        .OnDelete(DeleteBehavior.Restrict);
});
    modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.ProductToPartId)
    .HasColumnName("producttopart_id");
    modelBuilder.Entity<OrderDraft>().ToTable("order_drafts");

    modelBuilder.Entity<OrderDraftItem>().ToTable("order_draft_items");

    modelBuilder.Entity<OrderDraftItem>()
    .Property(e => e.ProductToPartId)
    .HasColumnName("producttopart_id");

modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.CustomerName)
    .HasColumnName("customer_name");

modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.CustomerCode)
    .HasColumnName("customer_code");

modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.TopPartId)
    .HasColumnName("top_part_id");

modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.RalColorId)
    .HasColumnName("ral_color_id");

modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.IsProduct)
    .HasColumnName("is_product");

modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.IsPart)
    .HasColumnName("is_part");

modelBuilder.Entity<CustomerCodeMap>()
    .Property(e => e.VersionId)
    .HasColumnName("version_id");
modelBuilder.Entity<Order>()
    .Property(e => e.OrderNumber)
    .HasColumnName("order_number");

modelBuilder.Entity<Order>()
    .Property(e => e.CustomerName)
    .HasColumnName("customer_name");

modelBuilder.Entity<Order>()
    .Property(e => e.Comment)
    .HasColumnName("comment");

modelBuilder.Entity<Order>()
    .Property(e => e.OrderDate)
    .HasColumnName("order_date");

modelBuilder.Entity<Order>()
    .Property(e => e.CreatedAt)
    .HasColumnName("created_at");

modelBuilder.Entity<Order>()
    .Property(e => e.IsActive)
    .HasColumnName("is_active");

modelBuilder.Entity<OrderItem>()
    .Property(e => e.CustomerCode)
    .HasColumnName("customer_code");

modelBuilder.Entity<OrderItem>()
    .Property(e => e.CustomerCodeMapId)
    .HasColumnName("customer_code_map_id");

modelBuilder.Entity<OrderItem>()
    .Property(e => e.OrderId)
    .HasColumnName("order_id");

modelBuilder.Entity<OrderItem>()
    .Property(e => e.IsActive)
    .HasColumnName("is_active");

modelBuilder.Entity<OrderItem>()
    .Property(e => e.VersionId)
    .HasColumnName("version_id");

modelBuilder.Entity<WorkflowDependency>()
    .HasIndex(x => new { x.NodeId, x.DependsOnNodeId })
    .IsUnique();

}
  

    }


}


