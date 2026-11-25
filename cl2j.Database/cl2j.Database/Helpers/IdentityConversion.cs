using System.Globalization;

namespace cl2j.Database.Helpers
{
    /// <summary>
    ///     Turns the scalar an insert returns into the type the caller asked for.
    ///
    ///     <para>
    ///     Each server reports the identity in its own type, and the caller declared another. MySQL's
    ///     <c>LAST_INSERT_ID()</c> is <c>BIGINT UNSIGNED</c>, which the driver hands back as
    ///     <c>ulong</c>; unboxing that to an <c>int</c> fails with an <c>InvalidCastException</c>, and
    ///     MySQL has no <c>CAST(... AS INT)</c> to ask for something else. Converting here lets a
    ///     provider return whatever its server gives.
    ///     </para>
    ///
    ///     <para>
    ///     A value of the requested type passes through untouched, whatever the type is (a Guid or a
    ///     string, for instance). <c>null</c> and <c>DBNull</c> are <c>default</c>. Anything else goes
    ///     through <see cref="Convert.ChangeType(object, Type, IFormatProvider)"/> to the type the
    ///     request wraps (<c>int?</c> converts to <c>int</c>), with the invariant culture so that the
    ///     machine's settings cannot change a number. A value that does not fit the target throws
    ///     <see cref="OverflowException"/>: a truncated key is worse than an error.
    ///     </para>
    /// </summary>
    internal static class IdentityConversion
    {
        internal static TOut? To<TOut>(object? value)
        {
            if (value is null or DBNull)
                return default;

            if (value is TOut same)
                return same;

            var target = Nullable.GetUnderlyingType(typeof(TOut)) ?? typeof(TOut);

            return (TOut)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        /// <summary>
        ///     The same conversion for a target known only at run time, such as the type of a key
        ///     property. Returns <c>null</c> for <c>null</c> and <c>DBNull</c>.
        /// </summary>
        internal static object? To(object? value, Type target)
        {
            if (value is null or DBNull)
                return null;

            if (target.IsInstanceOfType(value))
                return value;

            return Convert.ChangeType(value, Nullable.GetUnderlyingType(target) ?? target, CultureInfo.InvariantCulture);
        }
    }
}
