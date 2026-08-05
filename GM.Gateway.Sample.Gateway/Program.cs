using GM.Caching;
using GM.Caching.Redis;
using GM.DistributedLock;
using GM.Gateway;
using GM.RateLimiting.Http;
using GM.RateLimiting.Redis;

var builder = WebApplication.CreateBuilder(args);

// Counter store + concurrency. In-memory by default; set Redis:ConnectionString (or env
// Redis__ConnectionString=localhost:6379) to enforce limits across every gateway instance using the
// lock-free atomic Redis store.
var redisConnection = builder.Configuration.GetSection("Redis")["ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddGMRedisCaching(o => { o.ConnectionString = redisConnection; o.KeyPrefix = "gw-sample:"; });
    builder.Services.AddGMRedisRateLimitStore(redisConnection);
}
else
{
    builder.Services.AddGMCaching();
    builder.Services.AddGMDistributedLock();
}

// YARP reverse proxy + GM rate limiting, both from appsettings. Routes opt in to a policy via their
// Metadata (see appsettings.json).
builder.Services.AddGMGateway(
    builder.Configuration,
    configureHttp: o => o.DefaultPartition = RateLimitPartition.Ip);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "GM.Gateway sample",
    backend = string.IsNullOrWhiteSpace(redisConnection) ? "in-memory + lock" : "redis atomic",
    routes = new[]
    {
        "/orders/*   → orders-backend, rate limited 'api' (5/min per IP+endpoint)",
        "/products/* → products-backend, rate limited 'api' (own budget)",
        "/open/*     → products-backend, NOT rate limited",
    },
}));

app.UseRouting();
app.MapGMGateway();   // reverse proxy + per-route rate limiting

app.Run();

// Exposed so the test project can boot the gateway with WebApplicationFactory.
public partial class Program;
