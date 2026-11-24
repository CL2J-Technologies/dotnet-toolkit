namespace cl2j.Database.IntegrationTests
{
    /// <summary>
    ///     The hooks that <see cref="TypeMatrixTestsBase"/> and <see cref="TypeEdgeValueTestsBase"/>
    ///     share, declared once so that a provider reads one list. Not a test class itself.
    ///
    ///     <para>
    ///     Every default is SQL Server's own behaviour. A provider overrides a member only where
    ///     its server stores or returns something else, in <b>both</b> derived classes (C# has no
    ///     way to inherit the same override into two bases): put the overrides in a static helper
    ///     and have each derived class forward to it if the duplication bothers you.
    ///     </para>
    ///
    ///     <para>
    ///     The three value hooks work in this order on every temporal or otherwise lossy value
    ///     the tests write: <see cref="TemporalFractionalDigits"/> trims the fraction, then
    ///     <see cref="ValueToWrite"/> may replace a value the server cannot hold, then the row is
    ///     written, and what comes back is compared with <see cref="ExpectedValue"/> of what was
    ///     written.
    ///     </para>
    /// </summary>
    public abstract class TypeTestsBase(IDatabaseFixture fixture)
    {
        /// <summary>The fixture the derived class was built with.</summary>
        protected IDatabaseFixture Fixture => fixture;

        /// <summary>
        ///     How many fractional-second digits the provider's temporal columns keep, 0 to 7
        ///     (.NET ticks have seven). SQL Server: 7 (<c>datetime2</c>, <c>datetimeoffset</c> and
        ///     <c>time</c> default to scale 7). Override when your server stops short: MySQL's
        ///     <c>datetime(6)</c> and PostgreSQL's <c>timestamp</c> keep 6.
        ///
        ///     <para>
        ///     The tests trim the ticks of every <c>DateTime</c>, <c>DateTimeOffset</c>,
        ///     <c>TimeOnly</c> and <c>TimeSpan</c> they write to this many digits (truncating, never
        ///     rounding), so the value is one the server stores exactly and nothing depends on
        ///     whether it rounds or truncates. What the library does with a seventh digit is the
        ///     provider's own characterisation test to write.
        ///     </para>
        /// </summary>
        protected virtual int TemporalFractionalDigits => 7;

        /// <summary>
        ///     Replaces a value the provider's column cannot hold with one it can, before it is
        ///     written; the value has already been trimmed to <see cref="TemporalFractionalDigits"/>.
        ///     Return <paramref name="value"/> to keep it. SQL Server: always kept. Override for
        ///     the ranges: MySQL <c>datetime</c> starts in the year 1000, so
        ///     <see cref="DateTime.MinValue"/> and <see cref="DateOnly.MinValue"/> have to move.
        ///     Never called with <see langword="null"/>.
        /// </summary>
        /// <param name="value">What the test wants to write.</param>
        protected virtual object ValueToWrite(object value) => value;

        /// <summary>
        ///     What reading back the value that <see cref="ValueToWrite"/> returned yields. SQL
        ///     Server: the same value, offset included. Override when the column changes it:
        ///     PostgreSQL's <c>timestamptz</c> and MySQL's <c>datetime(6)</c> hold an instant, so a
        ///     <see cref="DateTimeOffset"/> comes back as <c>ToUniversalTime()</c>.
        ///     Never called with <see langword="null"/>.
        /// </summary>
        /// <param name="written">The value that was written, after <see cref="ValueToWrite"/>.</param>
        protected virtual object ExpectedValue(object written) => written;

        /// <summary>The value to write for <paramref name="wanted"/>: trimmed, then adjusted.</summary>
        protected T Written<T>(T wanted) where T : notnull
            => (T)ValueToWrite(TemporalPrecision.Trim(wanted, TemporalFractionalDigits));

        /// <summary>What <see cref="ExpectedValue"/> says comes back for <paramref name="written"/>.</summary>
        protected T Expected<T>(T written) where T : notnull
            => (T)ExpectedValue(written);
    }

    /// <summary>
    ///     Truncates a <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="TimeOnly"/>
    ///     or <see cref="TimeSpan"/> to the fractional digits a provider's column keeps, and
    ///     returns anything else untouched. Public for a provider's own overrides that need the
    ///     same rule.
    /// </summary>
    public static class TemporalPrecision
    {
        /// <summary>Truncates the ticks of <paramref name="value"/> below <paramref name="digits"/> fractional digits.</summary>
        public static object Trim(object value, int digits)
        {
            var unit = Unit(digits);
            return value switch
            {
                DateTime stamp => new DateTime(stamp.Ticks / unit * unit, stamp.Kind),
                DateTimeOffset moment => new DateTimeOffset(moment.Ticks / unit * unit, moment.Offset),
                TimeOnly time => new TimeOnly(time.Ticks / unit * unit),
                TimeSpan span => TimeSpan.FromTicks(span.Ticks / unit * unit),
                _ => value,
            };
        }

        /// <summary>Typed form of <see cref="Trim(object, int)"/>.</summary>
        public static T Trim<T>(T value, int digits) where T : notnull => (T)Trim((object)value, digits);

        private static long Unit(int digits)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(digits);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(digits, 7);
            var unit = 1L;
            for (var i = digits; i < 7; i++)
                unit *= 10;
            return unit;
        }
    }
}
