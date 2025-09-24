using TrackHive.Models;

namespace TrackHive.Services;

public static class RetentionPolicy
{
    public const int FreePlanRetentionDays = 90;

    public static DateTimeOffset? GetCutoff(OrganizationPlan plan, DateTimeOffset currentTime)
    {
        return plan == OrganizationPlan.Free
            ? currentTime.AddDays(-FreePlanRetentionDays)
            : null;
    }

    public static DateOnly? GetDateCutoff(OrganizationPlan plan, DateTimeOffset currentTime)
    {
        var cutoff = GetCutoff(plan, currentTime);
        return cutoff.HasValue
            ? DateOnly.FromDateTime(cutoff.Value.UtcDateTime.Date)
            : null;
    }
}
