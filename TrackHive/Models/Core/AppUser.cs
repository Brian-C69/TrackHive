using Microsoft.AspNetCore.Mvc;

using System;

namespace TrackHive.Models.Core
{
    public class AppUser
    {
        public Guid Id { get; set; }
        public Guid OrganizationId { get; set; }
        public Organization Organization { get; set; } = default!;

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // Simple for now; replace with Identity later
        public string PasswordHash { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; } = true;

        public UserRole Role { get; set; }
    }
}
