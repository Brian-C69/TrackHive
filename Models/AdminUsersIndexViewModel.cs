using System;
using System.Collections.Generic;

namespace TrackHive.Models;

public sealed class AdminUsersIndexViewModel
{
    public List<AdminUserRow> Users { get; set; } = new();
    public List<OrganizationOption> Organizations { get; set; } = new();
    public string? Query { get; set; }
    public int? OrganizationId { get; set; }
    public RoleType? Role { get; set; }
    public string Status { get; set; } = "any";
    public string SortBy { get; set; } = "created";
    public string SortDir { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }
}

public sealed class AdminUserRow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public RoleType Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class OrganizationOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
