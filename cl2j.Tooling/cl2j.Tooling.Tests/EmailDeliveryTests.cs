using System.Net;
using System.Net.Sockets;
using System.Text;
using cl2j.Tooling.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     The success path of <see cref="EmailService"/>, against a loopback server that speaks
    ///     just enough SMTP to accept one message. What is under test is what the service does with
    ///     a delivery the server accepted: report it and log it.
    /// </summary>
    public class EmailDeliveryTests
    {
        /// <summary>Accepts one connection, says yes to everything, and keeps the message it was given.</summary>
        private sealed class AcceptingServer : IDisposable
        {
            private readonly TcpListener listener = new(IPAddress.Loopback, 0);
            private readonly TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public AcceptingServer()
            {
                listener.Start();
                _ = Task.Run(RunAsync);
            }

            public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

            /// <summary>Completes with the DATA section as soon as the server has taken it.</summary>
            public async Task<string> ReceivedAsync() => await received.Task.WaitAsync(TimeSpan.FromSeconds(30));

            public void Dispose() => listener.Stop();

            private async Task RunAsync()
            {
                try
                {
                    await ServeAsync();
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or SocketException)
                {
                    //The client drops the connection without saying QUIT, and the test is over by then.
                }
            }

            private async Task ServeAsync()
            {
                using var client = await listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\r\n", AutoFlush = true };
                var data = new StringBuilder();

                await writer.WriteLineAsync("220 localhost ready");
                while (await reader.ReadLineAsync() is { } line)
                {
                    if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("354 go ahead");
                        while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                            data.AppendLine(dataLine);
                        received.TrySetResult(data.ToString());
                        await writer.WriteLineAsync("250 queued");
                    }
                    else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                    {
                        await writer.WriteLineAsync("221 bye");
                        break;
                    }
                    else
                    {
                        await writer.WriteLineAsync("250 ok");
                    }
                }
            }
        }

        private sealed class MinimalMailer : IEmailService
        {
            public string? LastSubject { get; private set; }

            public Task<EmailResult> SendEmailAsync(string subject, string body, string toEmail, bool isBodyHtml = false) => Task.FromResult(EmailResult.Success());

            public Task<EmailResult> SendEmailAsync(string from, string subject, string body, string toEmail, bool isBodyHtml = false)
            {
                LastSubject = subject;
                return Task.FromResult(EmailResult.Success());
            }

            public Task<EmailResult> SendSystemAsync(string subject, string details, bool isBodyHtml = false) => Task.FromResult(EmailResult.Success());

            public Task<EmailResult> SendErrorAsync(Exception ex, string subject, string details, bool isBodyHtml = false) => Task.FromResult(EmailResult.Success());
        }

        [Fact]
        public async Task A_message_the_server_accepts_is_reported_as_sent_and_logged()
        {
            using var server = new AcceptingServer();
            var logger = new RecordingLogger<EmailService>();
            var service = new EmailService(
                Options.Create(new SmtpSettings { Server = "127.0.0.1", Port = server.Port, User = "user", Password = "password", From = "sender@example.invalid" }),
                logger);

            var result = await service.SendEmailAsync("Greetings", "Hello there", "to@example.invalid");
            var received = await server.ReceivedAsync();

            Assert.True(result.Sent);
            Assert.Null(result.Failure);
            Assert.Contains("Hello there", received, StringComparison.Ordinal);
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("to@example.invalid", StringComparison.Ordinal));
        }

        [Fact]
        public async Task An_implementer_that_does_not_know_about_reply_to_still_sends_the_message()
        {
            IEmailService mailer = new MinimalMailer();

            var result = await mailer.SendEmailAsync("sender@example.invalid", "Subject", "body", "to@example.invalid", "reply@example.invalid");

            Assert.True(result.Sent);
            Assert.Equal("Subject", ((MinimalMailer)mailer).LastSubject);
        }
    }
}
