using Endpoints;
using Extensions;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddRedisCluster(builder.Configuration);

var app = builder.Build();

app.UseHttpsRedirection();
// Singletons are created lazily. Resolve it now so we connect at startup
// instead of making the first HTTP request pay the connection cost.
_ = app.Services.GetRequiredService<IConnectionMultiplexer>();

app.MapGet("/", () => "Redis Cluster demo API is running");
app.MapCacheEndpoints();
app.MapClusterEndpoints();
app.Run();
