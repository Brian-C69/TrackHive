using Microsoft.AspNetCore.Mvc;

using System;
using System.Collections.Generic;

namespace TrackHive.Models.Core
{
    public class Organization
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        // Pricing/plan
        public string Plan { get; set; } = "Free"; // Free/Starter/Pro/Enterprise
        public int EmployeeLimit { get; set; } = 5; // Free tier limit

        // Navigation
        public List<AppUser> Users { get; set; } = new();
    }
}
