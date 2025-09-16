// File: Models/EmailService.cs
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace TrackHive.Models;

/// <summary>SMTP wrapper. Keep simple for now.</summary>
public sealed class EmailService
{
    private readonly SmtpOptions _opt;
    public EmailService(IOptions<SmtpOptions> opt) => _opt = opt.Value;

    public async Task<(bool ok, string? error)> SendAsync(string to, string subject, string htmlBody)
    {
        try
        {
            using var client = new SmtpClient(_opt.Host, _opt.Port)
            {
                EnableSsl = _opt.EnableSsl,
                Credentials = new NetworkCredential(_opt.User, _opt.Pass),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(_opt.User, _opt.Name),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            msg.To.Add(new MailAddress(to));

            await client.SendMailAsync(msg);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}