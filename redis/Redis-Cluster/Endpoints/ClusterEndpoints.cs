using StackExchange.Redis;
using System.Net;

namespace Endpoints;

public static class ClusterEndpoints
{
    public static RouteGroupBuilder MapClusterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/cluster").WithTags("Cluster");

        group.MapGet("/nodes", GetNodes);
        group.MapGet("/where/{key}", WhereIsKeyAsync);

        return group;
    }

    private static IResult GetNodes(IConnectionMultiplexer redis)
    {
        var server = redis.GetServers().FirstOrDefault(s => s.IsConnected);
        if (server?.ClusterConfiguration is not { } config)
            return Results.Problem("No connected node could report the cluster configuration.", statusCode: 503);

        var nodes = config.Nodes
            .OrderBy(n => n.IsReplica)
            .ThenBy(n => Format(n.EndPoint))
            .Select(n => new
            {
                endpoint = Format(n.EndPoint),
                role = n.IsReplica ? "replica" : "master",
                replicaOf = n.IsReplica ? Format(n.Parent?.EndPoint) : null,
                connected = n.IsConnected,
                slots = n.Slots.Select(s => $"{s.From}-{s.To}")
            });

        return Results.Ok(nodes);
    }

    private static async Task<IResult> WhereIsKeyAsync(string key, IConnectionMultiplexer redis)
    {
        var endpoint = await redis.GetDatabase().IdentifyEndpointAsync(key);
        return Results.Ok(new { key, slot = redis.HashSlot(key), master = Format(endpoint) });
    }

    // With hostname announcement, endpoints are DnsEndPoints; their default ToString() is "Unspecified/localhost:7001"
    public static string? Format(EndPoint? ep) => ep switch
    {
        DnsEndPoint d => $"{d.Host}:{d.Port}",
        IPEndPoint i => i.ToString(),
        null => null,
        _ => ep.ToString()
    };
}
