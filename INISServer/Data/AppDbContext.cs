using Microsoft.EntityFrameworkCore;

namespace InisServer.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Friendship> Friendships => Set<Friendship>();
    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.Property(u => u.Username).HasMaxLength(32).IsRequired();
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(t => t.TokenHash);
            e.HasOne(t => t.User).WithMany(u => u.RefreshTokens).HasForeignKey(t => t.UserId);
        });

        b.Entity<Game>(e =>
        {
            e.Property(g => g.Status).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(g => g.Status);
            // jsonb on PostgreSQL; falls back to the provider default (e.g. text on Sqlite in tests).
            if (Database.IsNpgsql())
            {
                e.Property(g => g.StateJson).HasColumnType("jsonb");
                e.Property(g => g.SeatsJson).HasColumnType("jsonb");
            }
        });

        b.Entity<Friendship>(e =>
        {
            // A given ordered pair may only have one relationship row.
            e.HasIndex(f => new { f.RequesterId, f.AddresseeId }).IsUnique();
            e.HasOne(f => f.Requester).WithMany().HasForeignKey(f => f.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(f => f.Addressee).WithMany().HasForeignKey(f => f.AddresseeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
