using Microsoft.EntityFrameworkCore;

namespace TrackHive.Models;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    

        modelBuilder.Entity<PasswordReset>()
            .HasIndex(p => p.Token)
            .IsUnique();
    }
}