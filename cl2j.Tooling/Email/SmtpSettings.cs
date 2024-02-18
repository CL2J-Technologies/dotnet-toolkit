namespace cl2j.Tooling.Email
{
    public class SmtpSettings
    {
        public string Server { get; set; } = null!;
        public string User { get; set; } = null!;
        public string Password { get; set; } = null!;
        public int Port { get; set; }

        public string? From { get; set; }
        public string? ErrorTo { get; set; }
    }
}