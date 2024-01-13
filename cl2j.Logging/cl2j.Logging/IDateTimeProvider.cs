using System;

namespace cl2j.Logging
{
    internal interface IDateTimeProvider
    {
        DateTimeOffset Now();
    }
}