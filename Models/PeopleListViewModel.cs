// File: Models/PeopleListViewModel.cs
using System.Collections.Generic;

namespace TrackHive.Models;

public sealed class PeopleListViewModel
{
    public string ActiveTab { get; set; } = "employees"; // "employees" | "hr"
    public List<AppUser> Employees { get; set; } = new();
    public List<AppUser> HRs { get; set; } = new();

    // NEW: search + paging
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalCount { get; set; }

    // NEW: sorting
    public string SortBy { get; set; } = "created"; // "name" | "email" | "created"
    public string SortDir { get; set; } = "desc";   // "asc" | "desc"
}