using TrackHive.Models;

namespace TrackHive.Services;

public static class RetentionPolicy
{
    public const int FreePlanRetentionDays = 90;

    public static DateTimeOffset? GetCutoff(SubscriptionPlan plan, DateTimeOffset currentTime)
    {
        return plan == SubscriptionPlan.Free
            ? currentTime.AddDays(-FreePlanRetentionDays)
            : null;
    }

    public static DateOnly? GetDateCutoff(SubscriptionPlan plan, DateTimeOffset currentTime)
    {
        var cutoff = GetCutoff(plan, currentTime);
        return cutoff.HasValue
            ? DateOnly.FromDateTime(cutoff.Value.UtcDateTime.Date)
            : null;
    }
}
