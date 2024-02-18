namespace cl2j.Tooling.Email
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string from, string subject, string body, string to, bool isBodyHtml = false);
    }
}