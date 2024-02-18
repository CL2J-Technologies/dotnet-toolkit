using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace cl2j.Tooling.Email
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> logger;
        private readonly SmtpSettings smtpSettings;

        public EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService> logger)
        {
            this.logger = logger;
            this.smtpSettings = smtpSettings.Value;
        }

        public async Task<bool> SendEmailAsync(string from, string subject, string body, string to, bool isBodyHtml = false)
        {
            try
            {
                var message = CreateMailMessage(from, subject, body, to, isBodyHtml);
                var client = CreateSmtpClient(smtpSettings);
                await client.SendMailAsync(message);

                logger.LogInformation($"Sent email sent to '{to}' (from={from})");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Unexpected error while sending email to '{to}'");
                return false;
            }
        }

        private static SmtpClient CreateSmtpClient(SmtpSettings smtpSettings)
        {
            return new SmtpClient(smtpSettings.Server)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpSettings.User, smtpSettings.Password),
                Port = smtpSettings.Port
            };
        }

        private static MailMessage CreateMailMessage(string from, string subject, string body, string to, bool isBodyHtml)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(from, from),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            };
            foreach (var address in to.Split(","))
                mailMessage.To.Add(address);
            return mailMessage;
        }
    }
}