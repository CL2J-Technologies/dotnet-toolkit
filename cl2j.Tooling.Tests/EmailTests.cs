using cl2j.Tooling.Email;
using cl2j.Tooling.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     Everything about sending mail that does not require a server to be listening: the
    ///     configuration a caller must supply, the address validation, and what happens when the
    ///     send fails.
    ///
    ///     <para>
    ///     Deliberately not covered: a successful <c>SmtpClient.SendMailAsync</c>. Reaching it
    ///     means standing up an SMTP server in CI to assert that the framework can talk to it,
    ///     which tests .NET rather than this package. What is covered instead is every decision
    ///     this class makes before handing over, and the one it makes afterwards — swallowing the
    ///     failure and returning false.
    ///     </para>
    /// </summary>
    public class EmailServiceTests
    {
        //Nothing is listening here, so a send fails immediately rather than waiting on a timeout.
        //A hostname would cost a DNS lookup; a loopback address costs a refused connection.
        private static SmtpSettings Unreachable(string? from = "sender@example.invalid", string? systemTo = null, string? errorTo = null)
        {
            return new SmtpSettings
            {
                Server = "127.0.0.1",
                Port = 59999,
                User = "user",
                Password = "password",
                From = from,
                SystemTo = systemTo,
                ErrorTo = errorTo
            };
        }

        private static (EmailService Service, RecordingLogger<EmailService> Logger) Build(SmtpSettings settings)
        {
            var logger = new RecordingLogger<EmailService>();
            return (new EmailService(Options.Create(settings), logger), logger);
        }

        [Fact]
        public async Task Sending_without_a_configured_sender_says_which_setting_is_missing()
        {
            var (service, _) = Build(Unreachable(from: null));

            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => service.SendEmailAsync("subject", "body", "to@example.invalid"));

            Assert.Contains("From", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_system_message_needs_both_a_sender_and_a_system_recipient()
        {
            var (withoutFrom, _) = Build(Unreachable(from: null, systemTo: "ops@example.invalid"));
            await Assert.ThrowsAsync<ValidationException>(() => withoutFrom.SendSystemAsync("subject", "details"));

            var (withoutTo, _) = Build(Unreachable(systemTo: null));
            var exception = await Assert.ThrowsAsync<ValidationException>(() => withoutTo.SendSystemAsync("subject", "details"));
            Assert.Contains("SystemTo", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_error_message_needs_both_a_sender_and_an_error_recipient()
        {
            var (withoutFrom, _) = Build(Unreachable(from: null, errorTo: "ops@example.invalid"));
            await Assert.ThrowsAsync<ValidationException>(() => withoutFrom.SendErrorAsync(new InvalidOperationException(), "subject", "details"));

            var (withoutTo, _) = Build(Unreachable(errorTo: null));
            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => withoutTo.SendErrorAsync(new InvalidOperationException(), "subject", "details"));
            Assert.Contains("ErrorTo", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_send_that_fails_returns_false_and_is_logged()
        {
            //Worth being explicit about, because it is the contract every caller inherits: this
            //class never throws on a delivery failure. The bool is the only signal, and the reason
            //exists only in the log.
            var (service, logger) = Build(Unreachable());

            var sent = await service.SendEmailAsync("subject", "body", "to@example.invalid");

            Assert.False(sent);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Exception is not null);
        }

        [Fact]
        public async Task A_recipient_that_is_not_an_address_is_a_failure_rather_than_a_crash()
        {
            var (service, logger) = Build(Unreachable());

            var sent = await service.SendEmailAsync("subject", "body", "not an address");

            Assert.False(sent);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error);
        }

        [Fact]
        public async Task A_system_message_goes_to_the_system_recipient()
        {
            //Not a coverage exercise: this is the only assertion anywhere that SendSystemAsync
            //routes to SystemTo rather than to one of the other two configured addresses. The send
            //cannot succeed without a server, but the error log names the recipient it tried —
            //which is enough to prove where it was addressed.
            var (service, logger) = Build(Unreachable(
                from: "sender@example.invalid",
                systemTo: "ops@example.invalid",
                errorTo: "errors@example.invalid"));

            Assert.False(await service.SendSystemAsync("subject", "details"));

            var failure = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
            Assert.Contains("ops@example.invalid", failure.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("errors@example.invalid", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task An_error_message_goes_to_the_error_recipient()
        {
            var (service, logger) = Build(Unreachable(
                from: "sender@example.invalid",
                systemTo: "ops@example.invalid",
                errorTo: "errors@example.invalid"));

            Assert.False(await service.SendErrorAsync(new InvalidOperationException("boom"), "subject", "details"));

            Assert.Contains(logger.Entries, e =>
                e.Level == LogLevel.Error && e.Message.Contains("errors@example.invalid", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Entries, e =>
                e.Message.Contains("ops@example.invalid", StringComparison.Ordinal));
        }

        [Fact]
        public async Task An_error_report_carries_the_original_message_into_the_body()
        {
            //SendErrorAsync appends ex.Message to the details. Nothing else asserts that the
            //exception being reported actually reaches the message rather than only the log.
            var (service, logger) = Build(Unreachable(errorTo: "errors@example.invalid"));

            await service.SendErrorAsync(new InvalidOperationException("the original failure"), "subject", "details");

            Assert.Contains(logger.Entries, e =>
                e.Exception is InvalidOperationException { Message: "the original failure" });
        }

        [Fact]
        public async Task An_error_report_is_logged_before_it_is_sent()
        {
            //SendErrorAsync logs the exception itself, so a failure to deliver the report does not
            //also lose the thing being reported.
            var (service, logger) = Build(Unreachable(errorTo: "ops@example.invalid"));
            var reported = new InvalidOperationException("the original failure");

            await service.SendErrorAsync(reported, "subject", "details");

            Assert.Contains(logger.Entries, e => ReferenceEquals(e.Exception, reported));
        }
    }

    public class EmailAddressValidationTests
    {
        [Theory]
        [InlineData("simple@example.com")]
        [InlineData("first.last@example.com")]
        [InlineData("first+tag@example.co.uk")]
        [InlineData("MiXeD@Example.COM")]
        [InlineData("a@b.co")]
        [InlineData("user_name@sub.domain.example.com")]
        public void An_address_that_can_receive_mail_is_accepted(string email)
        {
            Assert.True(EmailExtensions.IsEmailValid(email));
        }

        [Theory]
        [InlineData("")]
        [InlineData("no-at-sign.example.com")]
        [InlineData("@example.com")]
        [InlineData("user@")]
        [InlineData("user@example")]
        [InlineData("user name@example.com")]
        [InlineData("user@exam ple.com")]
        [InlineData("user@@example.com")]
        [InlineData(".leading@example.com")]
        [InlineData("trailing.@example.com")]
        public void An_address_that_cannot_is_refused(string email)
        {
            Assert.False(EmailExtensions.IsEmailValid(email));
        }

        [Theory]
        [InlineData("user@example.com\nX-Injected: header")]
        [InlineData("user@example.com\r\nbcc: someone@example.com")]
        public void An_address_carrying_a_second_line_is_refused(string email)
        {
            //The anchors are \A and \Z rather than ^ and $, which in .NET would each match at a
            //line break and let a header-injection payload through as "valid".
            Assert.False(EmailExtensions.IsEmailValid(email));
        }

        [Fact]
        public void An_address_ending_in_a_newline_is_accepted()
        {
            //Characterisation, not endorsement. \Z matches at the end of the input *or* just before
            //a trailing newline — \z is the strict one — so a single trailing line break survives
            //validation. Reported in issue #31.
            Assert.True(EmailExtensions.IsEmailValid("user@example.com\n"));
        }

        [Fact]
        public void An_address_ending_in_a_newline_is_refused_where_it_is_used()
        {
            //Which is what keeps the line above a sharp edge rather than a hole: whatever
            //IsEmailValid says, the address never reaches a header.
            Assert.Throws<FormatException>(() => new System.Net.Mail.MailAddress("user@example.com\n"));
        }
    }

    public class EmailRegistrationTests
    {
        [Fact]
        public void Registering_email_resolves_a_service_configured_from_the_smtp_section()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SmtpSettings:Server"] = "smtp.example.invalid",
                    ["SmtpSettings:Port"] = "587",
                    ["SmtpSettings:From"] = "sender@example.invalid",
                    ["SmtpSettings:EnableSsl"] = "true"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddEmail(configuration);

            using var provider = services.BuildServiceProvider();

            Assert.IsType<EmailService>(provider.GetRequiredService<IEmailService>());

            var settings = provider.GetRequiredService<IOptions<SmtpSettings>>().Value;
            Assert.Equal("smtp.example.invalid", settings.Server);
            Assert.Equal(587, settings.Port);
            Assert.True(settings.EnableSsl);
        }

        [Fact]
        public void A_registration_that_is_already_there_is_left_alone()
        {
            //TryAddSingleton, not AddSingleton: an application that registers its own
            //IEmailService before calling AddEmail keeps it.
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IEmailService, NoopEmailService>();
            services.AddEmail(new ConfigurationBuilder().Build());

            using var provider = services.BuildServiceProvider();

            Assert.IsType<NoopEmailService>(provider.GetRequiredService<IEmailService>());
        }

        private sealed class NoopEmailService : IEmailService
        {
            public Task<bool> SendEmailAsync(string subject, string body, string toEmail, bool isBodyHtml = false) => Task.FromResult(true);

            public Task<bool> SendEmailAsync(string from, string subject, string body, string toEmail, bool isBodyHtml = false) => Task.FromResult(true);

            public Task<bool> SendSystemAsync(string subject, string details, bool isBodyHtml = false) => Task.FromResult(true);

            public Task<bool> SendErrorAsync(Exception ex, string subject, string details, bool isBodyHtml = false) => Task.FromResult(true);
        }
    }
}
