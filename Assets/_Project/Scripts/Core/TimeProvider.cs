using System;

namespace FarmGame.Core
{
    public interface ITimeProvider
    {
        long UtcNowSeconds { get; }
    }

    public sealed class SystemTimeProvider : ITimeProvider
    {
        public long UtcNowSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
