namespace TrackHive.Models;

public sealed record NavigationLink(
    string Id,
    string Label,
    string Icon,
    string Controller,
    string Action,
    string Section);
