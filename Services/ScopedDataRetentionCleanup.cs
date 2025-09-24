using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Services;

public sealed class ScopedDataRetentionCleanup
{
    private readonly AppDbContext _db;

    public ScopedDataRetentionCleanup(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> ApplyAsync(DateTimeOffset currentTime, CancellationToken cancellationToken = default)
    {
        var deleted = 0;
        var instantCutoff = RetentionPolicy.GetCutoff(SubscriptionPlan.Free, currentTime);
        var dateCutoff = RetentionPolicy.GetDateCutoff(SubscriptionPlan.Free, currentTime);

        if (dateCutoff is DateOnly dateOnlyCutoff)
        {
            deleted += await _db.AttendanceRecords
                .Where(r => r.User != null
                    && r.User.Organization != null
                    && r.User.Organization.CurrentPlan == SubscriptionPlan.Free
                    && r.Date < dateOnlyCutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        if (instantCutoff is DateTimeOffset cutoff)
        {
            deleted += await _db.LeaveDocuments
                .Where(d => d.LeaveRequest != null
                    && d.LeaveRequest.User != null
                    && d.LeaveRequest.User.Organization != null
                    && d.LeaveRequest.User.Organization.CurrentPlan == SubscriptionPlan.Free
                    && d.UploadedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            deleted += await _db.LeaveRequests
                .Where(r => r.User != null
                    && r.User.Organization != null
                    && r.User.Organization.CurrentPlan == SubscriptionPlan.Free
                    && r.CreatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            deleted += await _db.PayrollRecords
                .Where(r => r.User != null
                    && r.User.Organization != null
                    && r.User.Organization.CurrentPlan == SubscriptionPlan.Free
                    && r.CalculatedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return deleted;
    }
}
