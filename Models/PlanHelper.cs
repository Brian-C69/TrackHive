namespace TrackHive.Models;

public static class PlanHelper
{
    public const OrganizationPlan PayrollRequiredPlan = OrganizationPlan.Pro;
    public const OrganizationPlan AnalyticsRequiredPlan = OrganizationPlan.Pro;
    public const OrganizationPlan PdfRequiredPlan = OrganizationPlan.Starter;

    public static bool CanAccessPayroll(OrganizationPlan plan) => plan >= PayrollRequiredPlan;

    public static bool CanViewAnalytics(OrganizationPlan plan) => plan >= AnalyticsRequiredPlan;

    public static bool CanExportPdf(OrganizationPlan plan) => plan >= PdfRequiredPlan;

    public static bool RequiresUpgrade(OrganizationPlan plan, OrganizationPlan required) => plan < required;

    public static string GetDisplayName(OrganizationPlan plan) => plan switch
    {
        OrganizationPlan.Free => "Free",
        OrganizationPlan.Starter => "Starter",
        OrganizationPlan.Pro => "Pro",
        OrganizationPlan.Enterprise => "Enterprise",
        _ => plan.ToString()
    };
}
