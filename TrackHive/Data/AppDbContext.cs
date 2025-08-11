using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;
using TrackHive.Models.Core;

namespace TrackHive.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<AppUser> Users => Set<AppUser>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Organization>()
             .HasMany(o => o.Users)
             .WithOne(u => u.Organization)
             .HasForeignKey(u => u.OrganizationId);

            b.Entity<AppUser>()
             .HasIndex(u => new { u.OrganizationId, u.Email })
             .IsUnique();

            b.Entity<AppUser>()
             .Property(u => u.Role)
             .HasConversion<string>();
        }
    }
}

