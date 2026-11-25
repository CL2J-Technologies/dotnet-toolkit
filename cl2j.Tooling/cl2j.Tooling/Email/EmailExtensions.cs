using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace cl2j.Tooling.Email
{
    public static partial class EmailExtensions
    {
        //\z, not \Z: \Z also matches immediately before a single trailing newline, so
        //"user@example.com\n" validated. See issue #33.
        const string emailRex = @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\z";

        [GeneratedRegex(emailRex, RegexOptions.IgnoreCase, "en-CA")]
        private static partial Regex EmailRegex();

        public static void AddEmail(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SmtpSettings>(section => configuration.GetSection("SmtpSettings").Bind(section));
            services.TryAddSingleton<IEmailService, EmailService>();
        }

        public static bool IsEmailValid(string email)
        {
            return EmailRegex().IsMatch(email);
        }
    }
}