using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace cl2j.Tooling.Email
{
    public static class EmailExtensions
    {
        public static void AddEmail(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SmtpSettings>(section => configuration.GetSection("SmtpSettings").Bind(section));
            services.TryAddSingleton<IEmailService, EmailService>();
        }

        const string emailRex = @"\A(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*@(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)\Z";

        public static bool IsEmailValid(string email)
        {
            return Regex.IsMatch(email, emailRex, RegexOptions.IgnoreCase);
        }
    }
}