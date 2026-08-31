using GM.Caching;
using GM.Caching.Redis;
using GM.DistributedLock;
using GM.Gateway;
using GM.RateLimiting.Http;
using GM.RateLimiting.Redis;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

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

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    message = "GM.Gateway sample",
    backend = string.IsNullOrWhiteSpace(redisConnection) ? "in-memory + lock" : "redis atomic",
    routes = Program.Routes,
}));

// Liveness must not depend on downstream dependencies, so it runs no checks; readiness runs every
// registered health check (none here yet). See engineering baseline §11.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

app.UseRouting();
app.MapGMGateway();   // reverse proxy + per-route rate limiting

await app.RunAsync();

// Exposed so the test project can boot the gateway with WebApplicationFactory.
public partial class Program
{
    private static readonly string[] Routes =
    [
        "/orders/*   → orders-backend, rate limited 'api' (5/min per IP+endpoint)",
        "/products/* → products-backend, rate limited 'api' (own budget)",
        "/open/*     → products-backend, NOT rate limited",
    ];

    // Only used as a WebApplicationFactory<Program> marker; never instantiated directly.
    protected Program() { }
}
