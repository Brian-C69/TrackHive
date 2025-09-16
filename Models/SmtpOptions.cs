// File: Models/SmtpOptions.cs
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace TrackHive.Models;

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = string.Empty;   // SMTP username / from email
    public string Pass { get; set; } = string.Empty;   // SMTP password
    public string Name { get; set; } = "TrackHive";    // From display name
}