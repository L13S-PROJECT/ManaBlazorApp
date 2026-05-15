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
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        
{
    base.OnModelCreating(modelBuilder);
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

}
  

    }


}


