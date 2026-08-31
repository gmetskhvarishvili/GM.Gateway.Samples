# GM.Gateway.Samples

A runnable [GM.Gateway](https://github.com/gmetskhvarishvili/GM.Gateway) demo: a YARP reverse proxy
that enforces **GM distributed rate limiting at the edge**, in front of a small backend service.

> Consumes the published `GM.Gateway`, `GM.RateLimiting.Redis`, `GM.Caching`, `GM.Caching.Redis`, and
> `GM.DistributedLock` NuGet packages via `PackageReference` in `GM.Gateway.Sample.Gateway.csproj`.

## Layout

- **`GM.Gateway.Sample.Backend`** — a trivial downstream API (echoes the path).
- **`GM.Gateway.Sample.Gateway`** — the YARP gateway using `AddGMGateway()` + `MapGMGateway()`, with
  routes, clusters, and rate-limit policies in [`appsettings.json`](GM.Gateway.Sample.Gateway/appsettings.json).

Routes (all proxy to the backend cluster):

| Route | Path | Rate limit |
| --- | --- | --- |
| `orders` | `/orders/*` | policy `api` — fixed window 5/min per (IP, endpoint) |
| `products` | `/products/*` | policy `api` — own independent budget |
| `open` | `/open/*` | none — proxied, never limited |

Opt-in is pure config — the route's `Metadata`:

```json
"orders": {
  "ClusterId": "backend",
  "Match": { "Path": "/orders/{**catch-all}" },
  "Metadata": { "RateLimitPolicy": "api", "RateLimitPartition": "Ip,Endpoint" }
}
```

## Run

Two terminals:

```bash
# 1) backend on :5100 (the gateway's cluster points here)
dotnet run --project GM.Gateway.Sample.Backend --urls http://localhost:5100

# 2) gateway on :5000
dotnet run --project GM.Gateway.Sample.Gateway --urls http://localhost:5000
```

```bash
# 6th call within a minute is throttled by the gateway, before it reaches the backend
for i in $(seq 1 6); do curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/orders/42; done
# 200 200 200 200 200 429
```

The `429` comes from the gateway with `Retry-After`, `X-RateLimit-*`, and a ProblemDetails body — the
backend never sees the request. `/products/*` has its own budget; `/open/*` is never limited.

## Cross-instance (Redis)

Run several gateway instances behind a load balancer and limits still hold when you point them at the
lock-free atomic Redis store:

```bash
Redis__ConnectionString=localhost:6379 dotnet run --project GM.Gateway.Sample.Gateway
```

## Tests

```bash
dotnet test
```

`tests/GM.Gateway.Sample.Tests` boots the real gateway (its actual `appsettings.json`) and forwards to
a stub backend, asserting the `orders` route allows 5 then `429`s, and `/open/*` is proxied unlimited.
