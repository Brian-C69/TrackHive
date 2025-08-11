using System.Threading.Tasks;

namespace TrackHive.Services.Email
{
    public interface IEmailSender
    {
        Task SendAsync(string toEmail, string subject, string htmlBody);
    }
}

