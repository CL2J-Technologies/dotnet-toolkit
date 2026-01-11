using System.Net;
using System.Net.Mail;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace cl2j.Tooling.Email
{
    public class EmailService(IOptions<SmtpSettings> smtpSettings, ILogger<EmailService> logger) : IEmailService
    {
        private readonly SmtpSettings smtpSettings = smtpSettings.Value;

        public async Task<bool> SendEmailAsync(string subject, string body, string toEmail, bool isBodyHtml = false)
        {
            if (smtpSettings.From == null)
                throw new ValidationException("'From' Smtp configuration is missing");

            return await SendEmailAsync(smtpSettings.From, subject, body, toEmail, isBodyHtml);
        }

        public async Task<bool> SendEmailAsync(string from, string subject, string body, string toEmail, bool isBodyHtml = false)
        {
            try
            {
                var message = CreateMailMessage(from, subject, body, toEmail, isBodyHtml);
                var client = CreateSmtpClient(smtpSettings);
                await client.SendMailAsync(message);

                if (logger.IsEnabled(LogLevel.Information))
                    logger.LogInformation($"Sent email sent to '{toEmail}' (from={from})");
                return true;
            }
            catch (Exception ex)
            {
                if (logger.IsEnabled(LogLevel.Error))
                    logger.LogError(ex, $"Unexpected error while sending email to '{toEmail}'");
                return false;
            }
        }

        public async Task<bool> SendSystemAsync(string subject, string details, bool isBodyHtml = false)
        {
            if (smtpSettings.From == null)
                throw new ValidationException("'From' Smtp configuration is missing");
            if (smtpSettings.SystemTo == null)
                throw new ValidationException("'SystemTo' Smtp configuration is missing");

            return await SendEmailAsync(smtpSettings.From, subject, details, smtpSettings.SystemTo, isBodyHtml);
        }

        public async Task<bool> SendErrorAsync(Exception ex, string subject, string details, bool isBodyHtml = false)
        {
            if (smtpSettings.From == null)
                throw new ValidationException("'From' Smtp configuration is missing");
            if (smtpSettings.ErrorTo == null)
                throw new ValidationException("'ErrorTo' Smtp configuration is missing");

            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError(ex, $"{subject} : {details}");
            return await SendEmailAsync(smtpSettings.From, subject, details + Environment.NewLine + ex.Message, smtpSettings.ErrorTo, isBodyHtml);
        }

        private static SmtpClient CreateSmtpClient(SmtpSettings smtpSettings)
        {
            return new SmtpClient(smtpSettings.Server)
            {
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtpSettings.User, smtpSettings.Password),
                Port = smtpSettings.Port,
                EnableSsl = smtpSettings.EnableSsl
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