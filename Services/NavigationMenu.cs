using System;
using System.Collections.Generic;
using System.Linq;
using TrackHive.Models;

namespace TrackHive.Services;

public static class NavigationMenu
{
    public static IReadOnlyList<NavigationLink> GetLinksForRole(RoleType role) =>
        role switch
        {
            RoleType.IT => new List<NavigationLink>
            {
                new("it-dashboard", "IT Dashboard", "bi-speedometer2", "Dashboard", "Index", "Main"),
                new("users", "All users", "bi-diagram-3", "Users", "Index", "Admin"),
                new("people", "People", "bi-people", "Dashboard", "People", "Admin"),
                new("billing", "Billing & Plans", "bi-credit-card", "Billing", "Upgrade", "Admin"),
                new("preferences", "Preferences", "bi-sliders2", "Profile", "Preferences", "Admin"),
                new("profile", "My profile", "bi-person-circle", "Profile", "Index", "Admin"),
            },
            RoleType.HR => new List<NavigationLink>
            {
                new("hr-dashboard", "HR Dashboard", "bi-graph-up", "HrDashboard", "Index", "Main"),
                new("people", "People", "bi-people", "Dashboard", "People", "Admin"),
                new("payroll", "Payroll", "bi-cash-stack", "Payroll", "Index", "Admin"),
                new("preferences", "Preferences", "bi-sliders2", "Profile", "Preferences", "Admin"),
                new("profile", "My profile", "bi-person-circle", "Profile", "Index", "Admin"),
            },
            RoleType.Employee => new List<NavigationLink>
            {
                new("employee-dashboard", "Employee Dashboard", "bi-speedometer2", "EmployeeDashboard", "Index", "Main"),
                new("onboarding", "Onboarding", "bi-journal-check", "Home", "Onboarding", "Main"),
                new("preferences", "Preferences", "bi-sliders2", "Profile", "Preferences", "Account"),
                new("profile", "My profile", "bi-person-circle", "Profile", "Index", "Account"),
            },
            _ => Array.Empty<NavigationLink>()
        };

    public static IReadOnlyList<NavigationLink> ApplyOrder(IEnumerable<NavigationLink> items, string? order)
    {
        var baseList = items.ToList();
        if (baseList.Count == 0) return baseList;

        if (string.IsNullOrWhiteSpace(order))
        {
            return baseList;
        }

        var ids = order
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (ids.Count == 0)
        {
            return baseList;
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var lookup = baseList.ToDictionary(link => link.Id, comparer);
        var seen = new HashSet<string>(comparer);
        var ordered = new List<NavigationLink>(baseList.Count);

        foreach (var id in ids)
        {
            if (lookup.TryGetValue(id, out var link) && seen.Add(link.Id))
            {
                ordered.Add(link);
            }
        }

        foreach (var link in baseList)
        {
            if (seen.Add(link.Id))
            {
                ordered.Add(link);
            }
        }

        return ordered;
    }
}
