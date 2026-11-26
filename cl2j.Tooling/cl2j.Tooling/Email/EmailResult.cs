namespace cl2j.Tooling.Email
{
    /// <summary>
    ///     What became of a send.
    ///
    ///     <para>
    ///     These methods used to return <see cref="bool"/>, so a caller learned that delivery had
    ///     not happened and never why: a wrong password, an unreachable server and a rejected
    ///     recipient were the same value, and the reason existed only in a log line. Carrying the
    ///     failure back is the whole point of the type. See issue #33.
    ///     </para>
    ///
    ///     <para>
    ///     There is deliberately no implicit conversion to <see cref="bool"/>. It would keep
    ///     existing call sites compiling, which sounds kind until you notice that the callers who
    ///     most need to see <see cref="Failure"/> are exactly the ones who would never look.
    ///     </para>
    /// </summary>
    public readonly record struct EmailResult
    {
        private EmailResult(bool sent, Exception? failure)
        {
            Sent = sent;
            Failure = failure;
        }

        /// <summary>Whether the message was handed to the server without complaint.</summary>
        public bool Sent { get; }

        /// <summary>
        ///     Why it was not, or <see langword="null"/> when it was. Only the exceptions a
        ///     delivery attempt can legitimately produce reach here — a defect in the mailer is
        ///     raised rather than reported.
        /// </summary>
        public Exception? Failure { get; }

        public static EmailResult Success() => new(true, null);

        public static EmailResult Failed(Exception failure)
        {
            ArgumentNullException.ThrowIfNull(failure);
            return new EmailResult(false, failure);
        }
    }
}
