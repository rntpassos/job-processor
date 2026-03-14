using JobProcessor.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobProcessor.Infrastructure.Persistence;

public class JobDbContext : DbContext
{
    public JobDbContext(DbContextOptions<JobDbContext> options) : base(options) { }

    public DbSet<JobEntity> Jobs => Set<JobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(e =>
        {
            e.ToTable("jobs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Type).HasColumnName("type").HasMaxLength(100).IsRequired();
            e.Property(x => x.Payload).HasColumnName("payload");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.MaxAttempts).HasColumnName("max_attempts");
            e.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
            e.Property(x => x.ScheduledAt).HasColumnName("scheduled_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            e.Property(x => x.LastError).HasColumnName("last_error");

            e.HasIndex(x => x.Status).HasDatabaseName("idx_jobs_status");
            e.HasIndex(x => x.NextAttemptAt).HasDatabaseName("idx_jobs_next_attempt_at");
        });
    }
}
