using System.Collections.Concurrent;

public class DataStructures
{
    private static readonly DataStructures _instance = new DataStructures();
    public static DataStructures Instance => _instance;

    /*
        TTL sentinels.
        NO_EXPIRY means a key never dies on its own and lives until it is deleted.
        Ttl() borrows the Redis convention for its return value: -1 means "the key is
        here but has no expiry", and -2 means "the key isn't here (or has just expired)".
    */
    private const long NO_EXPIRY = -1;
    private const long TTL_NO_EXPIRY = -1;
    private const long TTL_NO_KEY = -2;

    /*
        Every key maps to a single Entry. Bundling the value together with its expiry
        timestamp (in Unix milliseconds) keeps the whole record in one atomic slot of
        the ConcurrentDictionary, so I never have to keep two separate dictionaries in
        sync with each other.
    */
    private sealed class Entry
    {
        public string Value { get; set; } = string.Empty;
        public long ExpiresAtUnixMs { get; set; } = NO_EXPIRY;
    }

    private readonly ConcurrentDictionary<string, Entry> _store = new();

    private DataStructures() { }

    /* Commands */

    public string Set(string key, string value, long ttlSeconds = NO_EXPIRY)
    {
        _store[key] = new Entry
        {
            Value = value,
            ExpiresAtUnixMs = ToExpiryTimestamp(ttlSeconds),
        };
        return value;
    }

    public string? Get(string key)
    {
        if (TryGetLive(key, out Entry entry))
        {
            return entry.Value;
        }
        return null;
    }

    public bool Delete(string key)
    {
        // A key that has already expired should report as "nothing to delete",
        // so sweep it first and only claim a deletion if it was actually alive.
        if (!TryGetLive(key, out _))
        {
            return false;
        }
        return _store.TryRemove(key, out _);
    }

    public bool Exists(string key)
    {
        return TryGetLive(key, out _);
    }

    public long Increment(string key, long amount)
    {
        return AddToValue(key, amount);
    }

    public long Decrement(string key, long amount)
    {
        return AddToValue(key, -amount);
    }

    public bool Expire(string key, long ttlSeconds)
    {
        if (!TryGetLive(key, out Entry entry))
        {
            return false;
        }
        entry.ExpiresAtUnixMs = ToExpiryTimestamp(ttlSeconds);
        return true;
    }

    public long Ttl(string key)
    {
        if (!TryGetLive(key, out Entry entry))
        {
            return TTL_NO_KEY;
        }
        if (entry.ExpiresAtUnixMs == NO_EXPIRY)
        {
            return TTL_NO_EXPIRY;
        }
        long remainingMs = entry.ExpiresAtUnixMs - NowUnixMs();
        return remainingMs <= 0 ? TTL_NO_KEY : remainingMs / 1000;
    }

    /* Helper methods */

    private long AddToValue(string key, long delta)
    {
        Entry updated = _store.AddOrUpdate(
            key,
            _ => new Entry { Value = delta.ToString() },
            (_, existing) =>
            {
                if (IsExpired(existing))
                {
                    // Treat an expired counter as if it were never here and start from zero.
                    return new Entry { Value = delta.ToString() };
                }
                long current = 0;
                Int64.TryParse(existing.Value, out current);
                // Counting does not refresh the clock, so carry the original expiry over.
                return new Entry
                {
                    Value = (current + delta).ToString(),
                    ExpiresAtUnixMs = existing.ExpiresAtUnixMs,
                };
            });
        return Int64.Parse(updated.Value);
    }

    private bool TryGetLive(string key, out Entry entry)
    {
        if (_store.TryGetValue(key, out entry!))
        {
            if (!IsExpired(entry))
            {
                return true;
            }
            // Lazy expiration: drop the dead key the moment somebody looks for it,
            // but only if nobody has slipped a fresh value into the slot meanwhile.
            _store.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        }
        entry = null!;
        return false;
    }

    private bool IsExpired(Entry entry)
    {
        return entry.ExpiresAtUnixMs != NO_EXPIRY && entry.ExpiresAtUnixMs <= NowUnixMs();
    }

    private long ToExpiryTimestamp(long ttlSeconds)
    {
        return ttlSeconds <= NO_EXPIRY ? NO_EXPIRY : NowUnixMs() + (ttlSeconds * 1000);
    }

    private static long NowUnixMs()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
