using Microsoft.EntityFrameworkCore;
using Raid.Memory.Models;
using System.Text.Json;

namespace Raid.Memory.Data;

/// <summary>
/// Entity Framework DbContext for Memory Agent data storage
/// </summary>
public class MemoryDbContext : DbContext
{
    public MemoryDbContext(DbContextOptions<MemoryDbContext> options) : base(options)
    {
    }

    public DbSet<Knowledge> Knowledge { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Knowledge entity
        modelBuilder.Entity<Knowledge>(entity =>
        {
            entity.ToTable("Knowledge", "Memory");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Domain)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Concept)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Rule)
                .HasMaxLength(2000)
                .IsRequired();

            entity.Property(e => e.Source)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.Confidence)
                .HasColumnType("float"); // Use float instead of real with precision

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.LastUsedAt);

            entity.Property(e => e.UsageCount)
                .HasDefaultValue(0);

            entity.Property(e => e.EmbeddingVector)
                .HasColumnType("nvarchar(max)");

            // Configure JSON properties
            entity.Property(e => e.Tags)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.RelatedKnowledgeIds)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>())
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.Metadata)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, object>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, object>())
                .HasColumnType("nvarchar(max)");

            // Indexes for performance
            entity.HasIndex(e => e.Domain);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Confidence);
            entity.HasIndex(e => e.UsageCount);
            entity.HasIndex(e => new { e.Domain, e.Concept });
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // This will be overridden by dependency injection configuration
            optionsBuilder.UseSqlServer();
        }
    }
}