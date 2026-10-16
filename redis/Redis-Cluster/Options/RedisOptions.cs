namespace Options;

public class RedisOptions
{
    public const string SectionName = "Redis";

    public string[] Endpoints { get; init; } = [];
    public int ConnectTimeoutMs { get; init; } = 5000;
    public int SyncTimeoutMs { get; init; } = 5000;
    public string ClientName { get; init; } = "redis-cluster-demo-api";
    public string Password { get; set; } = "change-me-strong-password";
}
