using RT.Comb;

namespace RT.Comb;

public static class EnsureOrderedProvider
{
    private static readonly CustomNoRepeatTimestampProvider SqlNoDupeProvider = new(4);
    private static readonly CustomNoRepeatTimestampProvider UnixNoDupeProvider = new(1);

    public static readonly ICombProvider Legacy = new SqlCombProvider(
        new SqlDateTimeStrategy(),
        customTimestampProvider: SqlNoDupeProvider.GetTimestamp);

    public static readonly ICombProvider Sql = new SqlCombProvider(
        new UnixDateTimeStrategy(),
        customTimestampProvider: UnixNoDupeProvider.GetTimestamp);

    public static readonly ICombProvider PostgreSql = new PostgreSqlCombProvider(
        new UnixDateTimeStrategy(),
        customTimestampProvider: UnixNoDupeProvider.GetTimestamp);
}

public sealed class CustomNoRepeatTimestampProvider
{
    private DateTime _lastValue = DateTime.MinValue;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public double IncrementMs { get; set; }

    public CustomNoRepeatTimestampProvider(double incrementMs = 4)
    {
        IncrementMs = incrementMs;
    }

    public DateTime GetTimestamp()
    {
        DateTime now = DateTime.UtcNow;
        _semaphore.Wait();
        try
        {
            if ((now - _lastValue).TotalMilliseconds < IncrementMs)
            {
                now = _lastValue.AddMilliseconds(IncrementMs);
            }

            _lastValue = now;
        }
        finally
        {
            _semaphore.Release();
        }

        return now;
    }
}
