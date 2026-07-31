# GeoIpServices

[![NuGet](https://img.shields.io/nuget/v/GeoIpServices.svg)](https://www.nuget.org/packages/GeoIpServices)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GeoIpServices.svg)](https://www.nuget.org/packages/GeoIpServices)
[![License](https://img.shields.io/github/license/prmeyn/GeoIpServices.svg)](https://github.com/prmeyn/GeoIpServices/blob/main/LICENSE)

**GeoIpServices** is an open-source C# library that provides geolocation information for IP addresses with MongoDB caching. It wraps third-party IP geolocation services (currently supporting IpStack) to reduce API usage and costs by serving repeat lookups from your own database.

## ✨ Features

- 🌍 **IP Geolocation Lookup** - Resolve IPv4 addresses to country, region, city, postal code, coordinates and spoken languages
- 💾 **MongoDB Caching** - Store geolocation data in your own MongoDB instance, expired automatically by a TTL index
- 🤝 **Request Coalescing** - Concurrent lookups of the same address share a single upstream call
- 🔄 **Priority & Retry** - Query providers in a configured order, with a bounded retry budget per lookup
- 🔌 **Extensible Architecture** - Add providers by implementing `IGeoIpProvider`
- ⚡ **Cost Effective** - Reduce API costs for high-traffic applications

## 📋 Prerequisites

- .NET 10.0 or later
- MongoDB instance (local or cloud-based like MongoDB Atlas)
- IpStack API key (get one at [ipstack.com](https://ipstack.com))
- [MongoDbService](https://www.nuget.org/packages/MongoDbService) package (automatically installed as dependency)

## 🚀 Getting Started

### Installation

Install the NuGet package using the .NET CLI:

```bash
dotnet add package GeoIpServices
```

Or via Package Manager Console:

```powershell
Install-Package GeoIpServices
```

### Configuration

Add the following configuration to your `appsettings.json`:

```json
{
  "MongoDbSettings": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "GeoIpDatabase"
  },
  "GeoIpSettings": {
    "Controls": {
      "CacheDurationInHours": 24,
      "MaxRoundRobinAttempts": 2,
      "Priority": [ "IpStack" ]
    },
    "IpStack": {
      "ApiPrefix": "https://api.ipstack.com/",
      "ApiPostfix": "?access_key=YOUR_IPSTACK_API_KEY_HERE",
      "TimeoutInSeconds": 10
    }
  }
}
```

**Configuration Options:**

| Option | Default | Description |
|--------|---------|-------------|
| `CacheDurationInHours` | 24 | How long cached geolocation data is retained, enforced by a MongoDB TTL index. Must be at least 1. |
| `MaxRoundRobinAttempts` | 1 | How many times a single lookup cycles through all providers before giving up. Must be at least 1. |
| `Priority` | Required | Providers to query, in order of preference (e.g. `["IpStack"]`). Order is significant. |
| `ApiPrefix` | Required | The IpStack API base address. A trailing slash is added if missing. |
| `ApiPostfix` | Required | Your IpStack API key, as `?access_key=…` — **keep this in user secrets or environment variables, not in source control** |
| `TimeoutInSeconds` | 10 | Per-request timeout for calls to IpStack. |

Configuration is validated during host startup, so a missing or invalid value fails the deploy rather than the first request.

> **Note:** Cache expiry is handled by a MongoDB TTL index on `ResponseTimeStampUTC`. Changing `CacheDurationInHours` updates the existing index in place on the next startup.

### Usage Example

Here's a complete example of a minimal API that returns geolocation information for the requesting IP:

```csharp
using GeoIpServices;
using Microsoft.AspNetCore.Mvc;
using MongoDbService;
using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register MongoDB and GeoIp services
builder.Services.AddMongoDbServices();
builder.Services.AddGeoIpServices();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoint to get geolocation info from visitor's IP
// You can inject either IGeoInfoService (interface) or GeoIpService (concrete class)
app.MapGet("/ipinfo", async ([FromServices] GeoIpService geoIpService, HttpRequest httpRequest, CancellationToken cancellationToken) =>
{
    var ipAddress = GetOriginIpV4(httpRequest);

    if (ipAddress is null)
    {
        return Results.BadRequest("Unable to determine IP address");
    }

    var geoInfo = await geoIpService.GetGeoIpInfoFromIpv4(ipAddress, cancellationToken);

    if (geoInfo is null)
    {
        return Results.NotFound("Geolocation information not available");
    }

    return Results.Ok(geoInfo);
})
.WithName("GetGeoIpInfoFromIpv4")
.WithOpenApi();

// Helper method to extract client IP from request
IPAddress? GetOriginIpV4(HttpRequest httpRequest)
{
    // X-Forwarded-For is a comma-separated list ("client, proxy1, proxy2"), so passing the whole header
    // value to TryParse fails as soon as there is more than one proxy in front of the app.
    var forwardedFor = httpRequest.Headers["X-Forwarded-For"].FirstOrDefault();
    var ipString = forwardedFor?.Split(',').FirstOrDefault()?.Trim();

    if (string.IsNullOrWhiteSpace(ipString) || !IPAddress.TryParse(ipString, out IPAddress? clientIpAddress))
    {
        return null;
    }

    if (clientIpAddress.AddressFamily == AddressFamily.InterNetworkV6)
    {
        // MapToIPv4() does not validate. For a native IPv6 address it simply reinterprets the last four
        // bytes, so 2001:db8::1 would silently become 0.0.0.1 - a meaningless lookup that spends API
        // quota and gets cached. Only unwrap addresses that genuinely are IPv4.
        return clientIpAddress.IsIPv4MappedToIPv6 ? clientIpAddress.MapToIPv4() : null;
    }

    return clientIpAddress;
}

app.Run();
```

> **Security:** `X-Forwarded-For` is client-supplied and trivially spoofed. In production, prefer ASP.NET Core's
> [Forwarded Headers Middleware](https://learn.microsoft.com/aspnet/core/host-and-deploy/proxy-load-balancer)
> configured with your `KnownProxies` / `KnownNetworks`, and read `HttpContext.Connection.RemoteIpAddress`.

### IPv6

Only IPv4 addresses, and IPv6 addresses that are IPv4-mapped (`::ffff:a.b.c.d`), are supported. Native IPv6
addresses return `null` and are logged as a warning. They are deliberately **not** narrowed to IPv4, because
doing so produces a fabricated address rather than an error.

### Privacy

IP addresses are personal data under the GDPR. This library stores each looked-up address as the document id
of its cache entry, and includes addresses in warning and error log messages. Set `CacheDurationInHours` in
line with your retention policy, and account for both the cache collection and your log sink in your record
of processing activities.

Your IpStack access key travels in the query string, as the IpStack API requires. On .NET 9 and later,
`IHttpClientFactory` redacts query strings in its logs by default, so the key is not written to your logs
unless you have set `System.Net.Http.DisableUriRedaction` (or `DOTNET_SYSTEM_NET_HTTP_DISABLEURIREDACTION`).
Note that other channels — ASP.NET Core HTTP logging, tracing exporters, or an egress proxy — may still
capture full URLs.

## 🔧 Troubleshooting

### Common Issues

**Issue: "IpStack rejected the request"**
- The log entry includes IpStack's error code and type — `invalid_access_key` (101), `usage_limit_reached` (104) and so on
- Verify your access key and remaining quota at [ipstack.com](https://ipstack.com)

**Issue: MongoDB connection errors**
- Verify MongoDB is running and accessible at the specified connection string
- Check firewall rules if using a remote MongoDB instance
- Ensure the database user has read/write permissions, including permission to create indexes

**Issue: Always getting fresh data (cache not working)**
- Check `CacheDurationInHours` is set appropriately
- Confirm the TTL index exists: `db.IpStackInfo.getIndexes()` should show `expireAfterSeconds` on `ResponseTimeStampUTC`
- MongoDB's TTL monitor runs roughly once a minute, so expiry is not instantaneous

**Issue: The host fails to start**
- Configuration is validated at startup. The exception message names the offending setting.
- Ensure all required values are present (`Priority`, `ApiPrefix`, `ApiPostfix`)
- Verify `ApiPostfix` starts with `?access_key=` and has a key after it
- Check that `Priority` contains only known provider names

## ⬆️ Upgrading

**From 10.x:**

- `GeoIpSettings:Controls:SessionTimeoutInSeconds` is no longer used and can be removed. The `GeoIpInfoSession`
  collection is no longer written to and can be dropped.
- The cache timestamp changed from `DateTimeOffset` to `DateTime` so that MongoDB's TTL index applies to it.
  Existing entries were written in the old format and will never expire, so drop the cache collection once —
  it is a pure cache and repopulates itself:
  ```javascript
  db.IpStackInfo.drop()
  db.GeoIpInfoSession.drop()
  ```
- `GetGeoIpInfoFromIpv4` takes an optional `CancellationToken`, and `AddGeoIpServices()` now returns
  `IServiceCollection`.
- `GeoIpInfo` gained city, region, continent, postal code, coordinate and EU-membership properties.
- Provider internals (`IpStackService`, `IpStackDbService` and the IpStack DTOs) are no longer public.
  Resolve `IGeoInfoService` or `GeoIpService` instead.

## 🤝 Contributing

We welcome contributions! If you find a bug or have an idea for improvement:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Run the test suite with `dotnet test`.

## 📝 License

This project is licensed under the GNU General Public License v3.0. See the [LICENSE](LICENSE) file for details.

## 🔗 Links

- [NuGet Package](https://www.nuget.org/packages/GeoIpServices)
- [GitHub Repository](https://github.com/prmeyn/GeoIpServices)
- [Report Issues](https://github.com/prmeyn/GeoIpServices/issues)

## 🙏 Acknowledgments

- Built with [MongoDbService](https://www.nuget.org/packages/MongoDbService)
- Powered by [IpStack API](https://ipstack.com)

---

Happy coding! 🚀🌐📚
