using Microsoft.EntityFrameworkCore;

namespace TrackHive.Models;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<PasswordReset> PasswordResets => Set<PasswordReset>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<LeaveBalance> LeaveBalances => Set<LeaveBalance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {


        modelBuilder.Entity<PasswordReset>()
            .HasIndex(p => p.Token)
            .IsUnique();

        modelBuilder.Entity<AttendanceRecord>()
            .HasIndex(a => new { a.UserId, a.Date })
            .IsUnique();

        modelBuilder.Entity<AttendanceRecord>()
            .HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AttendanceRecord>()
            .Property(a => a.Date)
            .HasColumnType("date");

        modelBuilder.Entity<LeaveBalance>()
            .HasIndex(l => l.UserId)
            .IsUnique();

        modelBuilder.Entity<LeaveBalance>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LeaveRequest>()
            .HasOne(l => l.User)
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LeaveRequest>()
            .HasOne(l => l.ReviewedBy)
            .WithMany()
            .HasForeignKey(l => l.ReviewedById)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<LeaveRequest>()
            .HasIndex(l => l.Status);

        modelBuilder.Entity<LeaveRequest>()
            .Property(l => l.StartDate)
            .HasColumnType("date");

        modelBuilder.Entity<LeaveRequest>()
            .Property(l => l.EndDate)
            .HasColumnType("date");
    }
}
