using Microsoft.EntityFrameworkCore;
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
    public DbSet<PayrollRecord> PayrollRecords => Set<PayrollRecord>();
    public DbSet<LeaveDocument> LeaveDocuments => Set<LeaveDocument>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

        modelBuilder.Entity<Organization>()
            .HasIndex(o => o.StripeSubscriptionId)
            .IsUnique()
            .HasFilter("[StripeSubscriptionId] IS NOT NULL");


        modelBuilder.Entity<Organization>()
            .Property(o => o.Plan)
            .HasDefaultValue(OrganizationPlan.Free);

        modelBuilder.Entity<AppUser>()
            .Property(u => u.BirthDate)
            .HasColumnType("date");

        modelBuilder.Entity<AppUser>()
            .Property(u => u.About)
            .HasMaxLength(1024);

        modelBuilder.Entity<AppUser>()
            .Property(u => u.ProfileImagePath)
            .HasMaxLength(256);

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

        modelBuilder.Entity<LeaveRequest>()
            .HasMany(l => l.Documents)
            .WithOne(d => d.LeaveRequest)
            .HasForeignKey(d => d.LeaveRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LeaveDocument>()
            .Property(d => d.OriginalFileName)
            .HasMaxLength(256);

        modelBuilder.Entity<LeaveDocument>()
            .Property(d => d.StoredFilePath)
            .HasMaxLength(260);

        modelBuilder.Entity<LeaveDocument>()
            .Property(d => d.ContentType)
            .HasMaxLength(128);

        modelBuilder.Entity<LeaveDocument>()
            .HasIndex(d => d.LeaveRequestId);

        modelBuilder.Entity<PayrollRecord>()
            .HasIndex(p => new { p.UserId, p.Year, p.Month })
            .IsUnique();

        modelBuilder.Entity<PayrollRecord>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
