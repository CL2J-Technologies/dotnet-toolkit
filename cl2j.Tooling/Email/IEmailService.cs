namespace cl2j.Tooling.Email
{
    public interface IEmailService
    {
        Task<EmailResult> SendEmailAsync(string subject, string body, string toEmail, bool isBodyHtml = false);
        Task<EmailResult> SendEmailAsync(string from, string subject, string body, string toEmail, bool isBodyHtml = false);

        Task<EmailResult> SendSystemAsync(string subject, string details, bool isBodyHtml = false);
        Task<EmailResult> SendErrorAsync(Exception ex, string subject, string details, bool isBodyHtml = false);
    }
}