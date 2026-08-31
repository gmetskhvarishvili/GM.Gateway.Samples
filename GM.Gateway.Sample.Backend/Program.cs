// A trivial downstream service the gateway proxies to. Echoes the path so you can see the proxy work.
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/orders/{**rest}", (string? rest) => Results.Ok(new { service = "orders-backend", path = $"/orders/{rest}", at = DateTimeOffset.UtcNow }));
app.MapGet("/products/{**rest}", (string? rest) => Results.Ok(new { service = "products-backend", path = $"/products/{rest}", at = DateTimeOffset.UtcNow }));
app.MapGet("/", () => Results.Ok(new { service = "backend", ok = true }));
app.MapFallback(context => context.Response.WriteAsJsonAsync(new { service = "backend", path = context.Request.Path.Value }));

// Liveness must not depend on downstream dependencies, so it runs no checks; readiness runs every
// registered health check (none here yet). See engineering baseline §11.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");

await app.RunAsync();
