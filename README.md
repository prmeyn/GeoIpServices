# GeoIpServices

[![NuGet](https://img.shields.io/nuget/v/GeoIpServices.svg)](https://www.nuget.org/packages/GeoIpServices)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GeoIpServices.svg)](https://www.nuget.org/packages/GeoIpServices)
[![License](https://img.shields.io/github/license/prmeyn/GeoIpServices.svg)](https://github.com/prmeyn/GeoIpServices/blob/main/LICENSE)

**GeoIpServices** is an open-source C# library that provides geolocation information for IP addresses with intelligent MongoDB caching. It wraps third-party IP geolocation services (currently supporting IpStack) to significantly reduce API usage and costs through smart caching and session management.

## ✨ Features

- 🌍 **IP Geolocation Lookup** - Convert IPv4 addresses to geographic information (country, region, city, etc.)
- 💾 **MongoDB Caching** - Store geolocation data in your own MongoDB instance to minimize external API calls
- 🔄 **Round-Robin Fallback** - Automatic retry mechanism with configurable priority-based provider queuing
- 📊 **Session Management** - Track lookup sessions to prevent duplicate queries and manage retries efficiently
- 🔌 **Extensible Architecture** - Easy to add support for additional IP geolocation providers
- ⚡ **Cost Effective** - Dramatically reduce API costs for high-traffic applications through intelligent caching

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
      "SessionTimeoutInSeconds": 240,
      "MaxRoundRobinAttempts": 2,
      "Priority": [ "IpStack" ]
    },
    "IpStack": {
      "ApiPrefix": "https://api.ipstack.com/",
      "ApiPostfix": "?access_key=YOUR_IPSTACK_API_KEY_HERE"
    }
  }
}
```

**Configuration Options:**

- `SessionTimeoutInSeconds`: How long to cache IP information before refreshing (default: 240 seconds)
- `MaxRoundRobinAttempts`: Number of retry attempts per provider (default: 2)
- `Priority`: Array of providers to use in order of preference
- `ApiPostfix`: Your IpStack API key - **Important:** Keep this in user secrets or environment variables in production!

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
app.MapGet("/ipinfo", async ([FromServices] GeoIpService geoIpService, HttpRequest httpRequest) =>
{
	var ipAddress = GetOriginIpV4(httpRequest);
	
	if (ipAddress == null)
	{
		return Results.BadRequest("Unable to determine IP address");
	}
	
	var geoInfo = await geoIpService.GetGeoIpInfoFromIpv4(ipAddress);
	
	if (geoInfo == null)
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
	var xForwardedForHeader = httpRequest.Headers["X-Forwarded-For"];
	var ipString = xForwardedForHeader.Select(s => s.Trim()).FirstOrDefault();
	
	if (string.IsNullOrWhiteSpace(ipString) || !IPAddress.TryParse(ipString, out IPAddress? clientIpAddress))
	{
		return null;
	}
	
	if (clientIpAddress.AddressFamily == AddressFamily.InterNetworkV6)
	{
		// If it's an IPv6 address, convert to IPv4
		return clientIpAddress.MapToIPv4();
	}
	
	return clientIpAddress;
}

app.Run();
```

## 🔧 Troubleshooting

### Common Issues

**Issue: "Unable to fetch info for IP"**
- Verify your IpStack API key is correct in `appsettings.json`
- Check that you haven't exceeded your IpStack API quota
- Ensure your MongoDB connection string is valid and the database is accessible

**Issue: MongoDB connection errors**
- Verify MongoDB is running and accessible at the specified connection string
- Check firewall rules if using a remote MongoDB instance
- Ensure the database user has read/write permissions

**Issue: Always getting fresh data (cache not working)**
- Check `SessionTimeoutInSeconds` is set appropriately
- Verify MongoDB is successfully storing session data
- Check application logs for any database write errors

## 🤝 Contributing

We welcome contributions! If you find a bug or have an idea for improvement:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

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
