using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace TrackHive.Models;

public sealed class UserPreferencesViewModel
{
    [Required]
    public string Theme { get; set; } = "light";

    [Required]
    [Display(Name = "Language")]
    public string Language { get; set; } = LanguagePreferences.DefaultLanguage;

    public List<SelectListItem> Languages { get; set; } = new();

    public string NavigationOrder { get; set; } = string.Empty;

    public List<NavigationLink> NavigationLinks { get; set; } = new();
}
