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
        public DbSet<ManiApi.Models.StepType> StepTypes { get; set; }

        public DbSet<ManiApi.Models.Employee> Employees { get; set; }
        public DbSet<ManiApi.Models.Tasks> Tasks { get; set; }

        public DbSet<ManiApi.Models.WorkCenter> WorkCentrs { get; set; }

        public DbSet<ManiApi.Models.StockMovement> StockMovements { get; set; }

        public DbSet<StageStepTypeMap> StageStepTypeMaps { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)

        
{
    base.OnModelCreating(modelBuilder);

    // <- PIESPIED tabulas nosaukumu ar underscore
    modelBuilder.Entity<StockMovement>().ToTable("stock_movements");

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
    
}
    public DbSet<BatchProduct> BatchProducts { get; set; }

    }


}


