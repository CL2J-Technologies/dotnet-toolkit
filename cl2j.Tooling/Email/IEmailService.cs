namespace cl2j.Tooling.Email
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string subject, string body, string toEmail, bool isBodyHtml = false);
        Task<bool> SendEmailAsync(string from, string subject, string body, string toEmail, bool isBodyHtml = false);

        Task<bool> SendSystemAsync(string subject, string details, bool isBodyHtml = false);
        Task<bool> SendErrorAsync(Exception ex, string subject, string details, bool isBodyHtml = false);
    }
}