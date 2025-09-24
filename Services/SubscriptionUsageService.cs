using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TrackHive.Models;

namespace TrackHive.Services;

public sealed class SubscriptionUsageService
{
    private static readonly SubscriptionPlanLimits DefaultLimits = new(null, null, "Contact support to adjust your subscription plan.");

    private static readonly IReadOnlyDictionary<SubscriptionPlan, SubscriptionPlanLimits> PlanLimits =
        new Dictionary<SubscriptionPlan, SubscriptionPlanLimits>
        {
            [SubscriptionPlan.Free] = new SubscriptionPlanLimits(
                HrLimit: 1,
                EmployeeLimit: 10,
                UpgradeMessage: "Upgrade from Settings → Billing to unlock more seats."),
            [SubscriptionPlan.Starter] = new SubscriptionPlanLimits(
                HrLimit: 3,
                EmployeeLimit: 25,
                UpgradeMessage: "Upgrade to the Pro plan to invite more teammates."),
            [SubscriptionPlan.Pro] = new SubscriptionPlanLimits(
                HrLimit: null,
                EmployeeLimit: null,
                UpgradeMessage: "Upgrade to the Enterprise plan for dedicated onboarding."),
            [SubscriptionPlan.Enterprise] = new SubscriptionPlanLimits(
                HrLimit: null,
                EmployeeLimit: null,
                UpgradeMessage: "You're already on the Enterprise plan.")
        };

    private readonly AppDbContext _db;

    public SubscriptionUsageService(AppDbContext db)
    {
        _db = db;
    }

    public SubscriptionPlanLimits GetLimits(SubscriptionPlan plan) =>
        PlanLimits.TryGetValue(plan, out var limits) ? limits : DefaultLimits;

    public async Task<SubscriptionUsage> GetUsageAsync(int organizationId, CancellationToken cancellationToken = default)
    {
        var plan = await _db.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => o.SubscriptionPlan)
            .FirstOrDefaultAsync(cancellationToken);

        var counts = await _db.Users.AsNoTracking()
            .Where(u => u.OrganizationId == organizationId && (u.Role == RoleType.HR || u.Role == RoleType.Employee))
            .GroupBy(u => u.Role)
            .Select(g => new RoleCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        var hrCount = counts.FirstOrDefault(c => c.Role == RoleType.HR)?.Count ?? 0;
        var employeeCount = counts.FirstOrDefault(c => c.Role == RoleType.Employee)?.Count ?? 0;

        var limits = GetLimits(plan);
        return new SubscriptionUsage(plan, hrCount, employeeCount, limits);
    }

    public Task<SubscriptionLimitCheckResult> CheckCanAddUserAsync(
        int organizationId,
        RoleType role,
        CancellationToken cancellationToken = default)
    {
        if (role is not (RoleType.HR or RoleType.Employee))
        {
            return Task.FromResult(SubscriptionLimitCheckResult.Allowed(role));
        }

        return CheckInternalAsync(organizationId, role, cancellationToken);
    }

    private async Task<SubscriptionLimitCheckResult> CheckInternalAsync(
        int organizationId,
        RoleType role,
        CancellationToken cancellationToken)
    {
        var usage = await GetUsageAsync(organizationId, cancellationToken);
        var (limit, count, seatLabel) = role == RoleType.HR
            ? (usage.Limits.HrLimit, usage.HrCount, "HR admins")
            : (usage.Limits.EmployeeLimit, usage.EmployeeCount, "employees");

        if (limit is null || count < limit.Value)
        {
            return SubscriptionLimitCheckResult.Allowed(role, usage, limit);
        }

        var message = BuildBlockMessage(usage.Plan, seatLabel, limit.Value, count, usage.Limits.UpgradeMessage);
        return SubscriptionLimitCheckResult.Blocked(role, usage, limit, message);
    }

    private static string BuildBlockMessage(
        SubscriptionPlan plan,
        string seatLabel,
        int limit,
        int current,
        string upgradeMessage)
    {
        var planName = plan.GetDisplayName();

        return $"Invite blocked: the {planName} plan allows up to {limit} {seatLabel} (currently {current}/{limit}). {upgradeMessage}";
    }

    private sealed record RoleCount(RoleType Role, int Count);
}

public sealed record SubscriptionPlanLimits(int? HrLimit, int? EmployeeLimit, string UpgradeMessage)
{
    public bool HasUnlimitedHr => HrLimit is null;
    public bool HasUnlimitedEmployees => EmployeeLimit is null;
}

public sealed record SubscriptionUsage(SubscriptionPlan Plan, int HrCount, int EmployeeCount, SubscriptionPlanLimits Limits);

public sealed record SubscriptionLimitCheckResult(
    bool CanAdd,
    RoleType Role,
    SubscriptionUsage? Usage,
    int? Limit,
    string? BlockReason)
{
    public static SubscriptionLimitCheckResult Allowed(
        RoleType role,
        SubscriptionUsage? usage = null,
        int? limit = null) => new(true, role, usage, limit, null);

    public static SubscriptionLimitCheckResult Blocked(
        RoleType role,
        SubscriptionUsage usage,
        int? limit,
        string blockReason) => new(false, role, usage, limit, blockReason);
}
