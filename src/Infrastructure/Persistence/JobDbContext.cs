using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class JobDbContext : DbContext
{
    public JobDbContext(DbContextOptions<JobDbContext> options) : base(options) { }

    public DbSet<JobEntity> Jobs => Set<JobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<JobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Type).HasMaxLength(100);
        });
    }
}
