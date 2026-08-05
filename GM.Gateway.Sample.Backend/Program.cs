// A trivial downstream service the gateway proxies to. Echoes the path so you can see the proxy work.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/orders/{**rest}", (string? rest) => Results.Ok(new { service = "orders-backend", path = $"/orders/{rest}", at = DateTimeOffset.UtcNow }));
app.MapGet("/products/{**rest}", (string? rest) => Results.Ok(new { service = "products-backend", path = $"/products/{rest}", at = DateTimeOffset.UtcNow }));
app.MapGet("/", () => Results.Ok(new { service = "backend", ok = true }));
app.MapFallback(context => context.Response.WriteAsJsonAsync(new { service = "backend", path = context.Request.Path.Value }));

app.Run();
