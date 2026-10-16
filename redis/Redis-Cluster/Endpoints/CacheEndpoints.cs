using Models;
using StackExchange.Redis;

namespace Endpoints;

public static class CacheEndpoints
{
    public static RouteGroupBuilder MapCacheEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cache").WithTags("Cache");

        group.MapPut("/{key}", SetAsync);
        group.MapGet("/{key}", GetAsync);
        group.MapDelete("/{key}", DeleteAsync);
        group.MapGet("/{key}/ttl", GetTtlAsync);

        return group;
    }

    private static async Task<IResult> SetAsync(string key, SetValueRequest request, IConnectionMultiplexer redis)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
            return Results.BadRequest(new { error = "Value is required." });
        if (request.TtlSeconds is <= 0)
            return Results.BadRequest(new { error = "TtlSeconds must be positive when provided." });

        var db = redis.GetDatabase();
        TimeSpan? ttl = request.TtlSeconds is int s ? TimeSpan.FromSeconds(s) : null;

        var stored = await db.StringSetAsync(
            key, request.Value,
            expiry: ttl,
            when: request.OnlyIfNotExists ? When.NotExists : When.Always);

        if (!stored)
            return Results.Conflict(new { key, error = "Key already exists (OnlyIfNotExists = true)." });

        return Results.Ok(new
        {
            key,
            stored,
            ttlSeconds = request.TtlSeconds,
            slot = redis.HashSlot(key),
            node = ClusterEndpoints.Format(await db.IdentifyEndpointAsync(key))
        });
    }

    private static async Task<IResult> GetAsync(string key, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        var value = await db.StringGetAsync(key);

        return value.IsNull
            ? Results.NotFound(new { key, error = "Key not found or expired." })
            : Results.Ok(new { key, value = value.ToString(), slot = redis.HashSlot(key) });
    }

    private static async Task<IResult> DeleteAsync(string key, IConnectionMultiplexer redis)
    {
        var deleted = await redis.GetDatabase().KeyDeleteAsync(key);
        return deleted ? Results.NoContent() : Results.NotFound(new { key });
    }

    private static async Task<IResult> GetTtlAsync(string key, IConnectionMultiplexer redis)
    {
        var db = redis.GetDatabase();
        var ttl = await db.KeyTimeToLiveAsync(key);

        // null TTL is ambiguous: key has no expiry OR key doesn't exist
        if (ttl is null)
            return await db.KeyExistsAsync(key)
                ? Results.Ok(new { key, ttl = "no expiry" })
                : Results.NotFound(new { key });

        return Results.Ok(new { key, ttlSeconds = Math.Round(ttl.Value.TotalSeconds) });
    }
}
