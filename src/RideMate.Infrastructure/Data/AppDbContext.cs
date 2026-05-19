using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RideMate.Domain.Entities;

namespace RideMate.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Circle> Circles => Set<Circle>();
    public DbSet<CircleMember> CircleMembers => Set<CircleMember>();
    public DbSet<LocationLog> LocationLogs => Set<LocationLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Circle>(entity =>
        {
            entity.HasIndex(c => c.InviteCode)
                .IsUnique();

            entity.Property(c => c.Name)
                .HasMaxLength(80);

            entity.Property(c => c.InviteCode)
                .HasMaxLength(12);
        });

        builder.Entity<CircleMember>(entity =>
        {
            entity.HasKey(cm => new { cm.CircleId, cm.UserId });

            entity.HasOne(cm => cm.Circle)
                .WithMany(c => c.Members)
                .HasForeignKey(cm => cm.CircleId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<ApplicationUser>()
                .WithMany(u => u.CircleMemberships)
                .HasForeignKey(cm => cm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<LocationLog>(entity =>
        {
            entity.Property(l => l.UserId)
                .HasMaxLength(450);

            entity.HasIndex(l => new { l.CircleId, l.Timestamp });
            entity.HasIndex(l => l.UserId);
        });
    }
}

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public string? AvatarUrl { get; set; }

    public bool IsLocationSharingEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CircleMember> CircleMemberships { get; set; }
        = new List<CircleMember>();
}
