namespace cl2j.Tooling.Email
{
    public interface IEmailService
    {
        Task<EmailResult> SendEmailAsync(string subject, string body, string toEmail, bool isBodyHtml = false);
        Task<EmailResult> SendEmailAsync(string from, string subject, string body, string toEmail, bool isBodyHtml = false);

        /// <summary>
        ///     Sends an email whose <c>Reply-To</c> header is <paramref name="replyTo" />, so the
        ///     recipient's "Reply" goes to that address instead of <paramref name="from" />.
        /// </summary>
        /// <param name="from">The sender address.</param>
        /// <param name="subject">The subject line.</param>
        /// <param name="body">The message body.</param>
        /// <param name="toEmail">The recipient address.</param>
        /// <param name="replyTo">The reply address. Null, empty or whitespace means no <c>Reply-To</c> header.</param>
        /// <param name="isBodyHtml">Whether the body is HTML rather than plain text.</param>
        /// <remarks>
        ///     The default implementation exists so that existing implementers of this interface keep
        ///     compiling. It ignores <paramref name="replyTo" /> and forwards to the overload without
        ///     it, which means the <c>Reply-To</c> header is silently dropped. An implementer that
        ///     sends real mail must override it. <see cref="EmailService" /> does.
        /// </remarks>
        Task<EmailResult> SendEmailAsync(string from, string subject, string body, string toEmail, string? replyTo, bool isBodyHtml = false)
            => SendEmailAsync(from, subject, body, toEmail, isBodyHtml);

        Task<EmailResult> SendSystemAsync(string subject, string details, bool isBodyHtml = false);
        Task<EmailResult> SendErrorAsync(Exception ex, string subject, string details, bool isBodyHtml = false);
    }
}