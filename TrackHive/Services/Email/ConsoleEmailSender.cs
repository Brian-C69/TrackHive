using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TrackHive.Services.Email
{
    public class ConsoleEmailSender : IEmailSender
    {
        private readonly ILogger<ConsoleEmailSender> _logger;
        public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) { _logger = logger; }

        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            _logger.LogInformation("EMAIL -> {To}\nSUBJECT: {Subject}\nBODY:\n{Body}", toEmail, subject, htmlBody);
            return Task.CompletedTask;
        }
    }
}
