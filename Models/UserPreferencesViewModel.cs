using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TrackHive.Models;

public sealed class UserPreferencesViewModel
{
    [Required]
    public string Theme { get; set; } = "light";

    public string NavigationOrder { get; set; } = string.Empty;

    public List<NavigationLink> NavigationLinks { get; set; } = new();
}
