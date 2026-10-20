using CRISP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
namespace CRISP.Infrastructure.Persistence;

public sealed class CrispDbContext(DbContextOptions<CrispDbContext> options) : DbContext(options)
{
    public DbSet<IndustryDetails> IndustryDetails => Set<IndustryDetails>();
    public DbSet<CustomerMaster> CustomerMasters => Set<CustomerMaster>();
    public DbSet<ParameterDefinitionMaster> ParameterDefinitionMasters => Set<ParameterDefinitionMaster>();
    public DbSet<WorkflowJsonVersion> WorkflowJsonVersions => Set<WorkflowJsonVersion>();
    public DbSet<IndustryParameterMapping> IndustryParameterMappings => Set<IndustryParameterMapping>();
    public DbSet<ValidationDefinition> ValidationDefinitions => Set<ValidationDefinition>();
    public DbSet<RuleDefinition> RuleDefinitions => Set<RuleDefinition>();
    public DbSet<ScaleConfiguration> ScaleConfigurations => Set<ScaleConfiguration>();
    public DbSet<ScaleConfigurationDetails> ScaleConfigurationDetails => Set<ScaleConfigurationDetails>();
    public DbSet<AssessmentResponse> AssessmentResponses => Set<AssessmentResponse>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<IndustryDetails>(e => {
            e.ToTable("IndustryDetails"); e.HasKey(x => x.IndustryDetailId); e.Property(x => x.IndustryName).HasMaxLength(256).IsUnicode(false).IsRequired();
        });
        b.Entity<CustomerMaster>(e =>
        {
            e.ToTable("CustomerMaster");

            e.HasKey(x => x.EntityId);

            e.Property(x => x.EntityName)
                .HasMaxLength(256)
                .IsUnicode(false)
                .IsRequired();

            e.Property(x => x.IsActive)
                .IsRequired();
        });
        b.Entity<ParameterDefinitionMaster>(e => {
            e.ToTable("ParameterDefinitionMaster"); e.HasKey(x => x.ParameterDefinitionMasterId); e.Property(x => x.ParameterCode).HasMaxLength(128).IsUnicode(false).IsRequired(); e.Property(x => x.ParameterName).HasMaxLength(256).IsUnicode(false).IsRequired(); e.Property(x => x.WhyItMatters).HasMaxLength(256).IsUnicode(false);
        });
        b.Entity<WorkflowJsonVersion>(e =>
        {
            e.ToTable("WorkflowJsonVersion");

            e.HasKey(x => x.WorkflowJsonVersionId);

            e.Property(x => x.WorkflowJsonVersionId)
                .ValueGeneratedOnAdd();

            e.Property(x => x.IndustryDetailsId)
                .IsRequired();

            e.Property(x => x.WorkflowCode)
                .HasMaxLength(128)
                .IsUnicode(false)
                .IsRequired();

            e.Property(x => x.WorkflowName)
                .HasMaxLength(256)
                .IsUnicode(false)
                .IsRequired();

            e.Property(x => x.VersionNo)
                .IsRequired();

            e.Property(x => x.JsonContent)
                .HasColumnType("varchar(max)")
                .IsRequired();

            e.Property(x => x.VersionName)
                .HasMaxLength(128)
                .IsUnicode(false)
                .IsRequired();

            e.Property(x => x.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .IsRequired();

            e.Property(x => x.IsPublished)
                .IsRequired();

            // IMPORTANT:
            // Explicitly bind the existing navigation properties
            // to IndustryDetailsId.
            e.HasOne(x => x.IndustryDetails)
                .WithMany(x => x.WorkflowJsonVersions)
                .HasForeignKey(x => x.IndustryDetailsId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new
            {
                x.WorkflowCode,
                x.VersionNo,
                x.IndustryDetailsId
            }).IsUnique();
        });
        b.Entity<IndustryParameterMapping>(e => {
            e.ToTable("IndustryParameterMapping"); e.HasKey(x => x.IndustryParameterMappingId); e.Property(x => x.ParameterDisplayName).HasMaxLength(128).IsUnicode(false); e.Property(x => x.ValueType).HasMaxLength(128).IsUnicode(false); e.Property(x => x.Weight).HasPrecision(10, 2); e.Property(x => x.DisplayOrder).HasDefaultValue(9999); e.HasOne<WorkflowJsonVersion>().WithMany().HasForeignKey(x => x.WorkflowJsonVersionId).OnDelete(DeleteBehavior.Cascade); e.HasOne<IndustryDetails>().WithMany().HasForeignKey(x => x.IndustryDetailsId).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new { x.WorkflowJsonVersionId, x.IndustryDetailsId, x.ParameterDefinitionMasterId }).IsUnique(); e.HasOne<ParameterDefinitionMaster>().WithMany().HasForeignKey(x => x.ParameterDefinitionMasterId).OnDelete(DeleteBehavior.Restrict); e.HasOne<IndustryParameterMapping>().WithMany().HasForeignKey(x => x.ParentIndustryParameterMappingId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ValidationDefinition>(e => {
            e.ToTable("ValidationDefinition"); e.HasKey(x => x.ValidationDefinitionId); e.Property(x => x.ValidationName).HasMaxLength(128).IsUnicode(false); e.Property(x => x.Expression).HasColumnType("varchar(max)"); e.Property(x => x.ErrorMessage).HasMaxLength(256).IsUnicode(false); e.HasOne<IndustryParameterMapping>().WithMany().HasForeignKey(x => x.IndustryParameterMappingId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<RuleDefinition>(e => {
            e.ToTable("RuleDefinition"); e.HasKey(x => x.RuleDefinitionId); e.Property(x => x.RuleName).HasMaxLength(128).IsUnicode(false); e.Property(x => x.RuleExpressionType).HasMaxLength(128).IsUnicode(false); e.Property(x => x.OutputExpression).HasMaxLength(256).IsUnicode(false); e.HasOne<WorkflowJsonVersion>().WithMany().HasForeignKey(x => x.WorkflowJsonVersionId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ScaleConfiguration>(e => {
            e.ToTable("ScaleConfiguration"); e.HasKey(x => x.ScaleConfigurationId); e.Property(x => x.ScaleCode).HasMaxLength(128).IsUnicode(false); e.Property(x => x.ScaleName).HasMaxLength(256).IsUnicode(false); e.Property(x => x.MinValue).HasPrecision(10, 2); e.Property(x => x.MaxValue).HasPrecision(10, 2);
        });
        b.Entity<ScaleConfigurationDetails>(e => {
            e.ToTable("ScaleConfigurationDetails"); e.HasKey(x => x.ScaleConfigurationDetailsId); e.Property(x => x.ScoreValue).HasPrecision(10, 2); e.Property(x => x.Definition).HasMaxLength(256).IsUnicode(false); e.Property(x => x.MinValue).HasPrecision(10, 2); e.Property(x => x.MaxValue).HasPrecision(10, 2); e.HasOne<IndustryParameterMapping>().WithMany().HasForeignKey(x => x.IndustryParameterMappingId).OnDelete(DeleteBehavior.Cascade); e.HasOne<ScaleConfiguration>().WithMany().HasForeignKey(x => x.ScaleConfigurationId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AssessmentResponse>(e => {
            e.ToTable("AssessmentResponse"); e.HasKey(x => x.AssessmentResponseId); e.Property(x => x.ResponseJson).HasColumnType("nvarchar(max)").IsRequired(); e.Property(x => x.Status).HasMaxLength(30).IsUnicode(false); e.HasOne(x => x.CustomerMaster).WithMany().HasForeignKey(x => x.EntityId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.WorkflowJsonVersion).WithMany().HasForeignKey(x => x.WorkflowJsonVersionId).OnDelete(DeleteBehavior.Restrict); e.HasIndex(x => new {
                x.EntityId,
                x.WorkflowJsonVersionId
            });
        });
    }
}