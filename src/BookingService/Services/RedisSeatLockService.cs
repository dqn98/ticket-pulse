using StackExchange.Redis;

namespace BookingService.Services;

public class RedisSeatLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisSeatLockService> _logger;

    public RedisSeatLockService(IConnectionMultiplexer redis, ILogger<RedisSeatLockService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<bool> AcquireLockAsync(Guid seatId, Guid eventId, string userId, TimeSpan expiry)
    {
        var db = _redis.GetDatabase();
        var key = $"seatlock:{eventId}:{seatId}";
        
        // Use userId as the lock value so we know who holds the lock
        bool acquired = await db.StringSetAsync(key, userId, expiry, When.NotExists);
        if (acquired)
        {
            _logger.LogInformation("Lock acquired for seat {SeatId} on event {EventId} by user {UserId}", seatId, eventId, userId);
        }
        return acquired;
    }

    public async Task<bool> ReleaseLockAsync(Guid seatId, Guid eventId, string userId)
    {
        var db = _redis.GetDatabase();
        var key = $"seatlock:{eventId}:{seatId}";
        
        var value = await db.StringGetAsync(key);
        if (value == userId)
        {
            return await db.KeyDeleteAsync(key);
        }
        return false;
    }
    
    public async Task<string?> GetLockOwnerAsync(Guid seatId, Guid eventId)
    {
        var db = _redis.GetDatabase();
        var key = $"seatlock:{eventId}:{seatId}";
        
        return await db.StringGetAsync(key);
    }
}
