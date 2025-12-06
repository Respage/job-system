using Microsoft.EntityFrameworkCore;
using JobSystem.Manager.Data.Entities;

namespace JobSystem.Manager.Data;

public class JobSystemDbContext : DbContext
{
    public JobSystemDbContext(DbContextOptions<JobSystemDbContext> options) : base(options)
    {
    }

    public DbSet<JobDefinition> JobDefinitions => Set<JobDefinition>();
    public DbSet<WorkerInstance> WorkerInstances => Set<WorkerInstance>();
    public DbSet<WorkerStatusReport> WorkerStatusReports => Set<WorkerStatusReport>();
    public DbSet<ScalingEvent> ScalingEvents => Set<ScalingEvent>();
    public DbSet<CodeSyncHistory> CodeSyncHistory => Set<CodeSyncHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // JobDefinition configuration
        modelBuilder.Entity<JobDefinition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Language).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.QueueType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.QueueConnectionString).HasMaxLength(500).IsRequired();
            entity.Property(e => e.QueueName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.InstanceSize).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ScaleMode).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.GitRepoUrl).HasMaxLength(500).IsRequired();
            entity.Property(e => e.GitBranch).HasMaxLength(100).HasDefaultValue("main");
            entity.Property(e => e.S3CodePath).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ApiKey).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // WorkerInstance configuration
        modelBuilder.Entity<WorkerInstance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.Ec2InstanceId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PrivateIpAddress).HasMaxLength(50);
            entity.Property(e => e.InstanceType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.LaunchedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.JobDefinition)
                .WithMany(j => j.WorkerInstances)
                .HasForeignKey(e => e.JobDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // WorkerStatusReport configuration
        modelBuilder.Entity<WorkerStatusReport>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ItemId).HasMaxLength(200);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.ReportedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.WorkerInstance)
                .WithMany(w => w.StatusReports)
                .HasForeignKey(e => e.WorkerInstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ScalingEvent configuration
        modelBuilder.Entity<ScalingEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.OccurredAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.JobDefinition)
                .WithMany(j => j.ScalingEvents)
                .HasForeignKey(e => e.JobDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // CodeSyncHistory configuration
        modelBuilder.Entity<CodeSyncHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GitCommitHash).HasMaxLength(40);
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.StartedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.JobDefinition)
                .WithMany(j => j.CodeSyncHistory)
                .HasForeignKey(e => e.JobDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
