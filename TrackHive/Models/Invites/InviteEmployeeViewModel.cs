using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models.Invites
{
    public class InviteEmployeeViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, Display(Name = "Full Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Department (optional)")]
        public string? Department { get; set; }
    }
}
