namespace TrackHive.Models;

public static class PlanHelper
{
    public const SubscriptionPlan PayrollRequiredPlan = SubscriptionPlan.Pro;
    public const SubscriptionPlan AnalyticsRequiredPlan = SubscriptionPlan.Pro;
    public const SubscriptionPlan PdfRequiredPlan = SubscriptionPlan.Starter;

    public static bool CanAccessPayroll(SubscriptionPlan plan) => plan >= PayrollRequiredPlan;

    public static bool CanViewAnalytics(SubscriptionPlan plan) => plan >= AnalyticsRequiredPlan;

    public static bool CanExportPdf(SubscriptionPlan plan) => plan >= PdfRequiredPlan;

    public static bool RequiresUpgrade(SubscriptionPlan plan, SubscriptionPlan required) => plan < required;

    public static string GetDisplayName(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Free => "Free",
        SubscriptionPlan.Starter => "Starter",
        SubscriptionPlan.Pro => "Pro",
        SubscriptionPlan.Enterprise => "Enterprise",
        _ => plan.ToString()
    };
}
