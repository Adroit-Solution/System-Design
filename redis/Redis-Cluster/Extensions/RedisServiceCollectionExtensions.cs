using Microsoft.Extensions.Options;
using Options;
using StackExchange.Redis;

namespace Extensions;

public static class RedisServiceCollectionExtensions
{
    public static IServiceCollection AddRedisCluster(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<RedisOptions>(config.GetSection(RedisOptions.SectionName));

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Redis");

            if (opts.Endpoints.Length == 0)
                throw new InvalidOperationException("Redis:Endpoints must contain at least one node.");

            var cfg = new ConfigurationOptions
            {
                AbortOnConnectFail = false,   // don't crash if a node is down at startup; keep retrying
                ConnectRetry = 3,
                ConnectTimeout = opts.ConnectTimeoutMs,
                SyncTimeout = opts.SyncTimeoutMs,
                AsyncTimeout = opts.SyncTimeoutMs,
                KeepAlive = 10,
                ReconnectRetryPolicy = new ExponentialRetry(1000, 10000),
                ClientName = opts.ClientName,
                Password = opts.Password,
            };
            foreach (var ep in opts.Endpoints)
                cfg.EndPoints.Add(ep);

            var muxer = ConnectionMultiplexer.Connect(cfg);

            // These events make the cluster's behaviour visible in the console (great for the failover demo)
            muxer.ConnectionFailed += (_, e) =>
                logger.LogWarning("Connection FAILED to {Endpoint} ({FailureType})", e.EndPoint, e.FailureType);
            muxer.ConnectionRestored += (_, e) =>
                logger.LogInformation("Connection RESTORED to {Endpoint}", e.EndPoint);
            muxer.ConfigurationChanged += (_, e) =>
                logger.LogInformation("Cluster topology changed, reported by {Endpoint}", e.EndPoint);
            muxer.HashSlotMoved += (_, e) =>
                logger.LogInformation("Slot {Slot} moved {Old} -> {New}", e.HashSlot, e.OldEndPoint, e.NewEndPoint);

            logger.LogInformation("Connected to Redis cluster. {Count} servers discovered.", muxer.GetServers().Length);
            return muxer;
        });

        return services;
    }
}
