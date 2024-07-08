 # GeoIpServices(https://www.nuget.org/packages/GeoIpServices)

**GeoIpServices** is an open-source C# class library that provides a wrapper around existing services that serve information about the IP. The service stores information in a MongoDb database that you configure using the package [MongoDbService](https://www.nuget.org/packages/MongoDbService) 

## Features

- Covers IpStack
- Cache information in your own MongoDB instance to reduce usage of the various services


## Contributing

We welcome contributions! If you find a bug, have an idea for improvement, please submit an issue or a pull request on GitHub.

## Getting Started

### [NuGet Package](https://www.nuget.org/packages/GeoIpServices)

To include **GeoIpServices** in your project, [install the NuGet package](https://www.nuget.org/packages/GeoIpServices):

```bash
dotnet add package GeoIpServices
```
Then in your `appsettings.json` add the following sample configuration and change the values to mtch the details of your MongoDB instance.
```json
  "GeoIpSettings": {
    "Controls": {
      "SessionTimeoutInSeconds": 240,
      "MaxRoundRobinAttempts": 2,
      "Priority": [ "IpStack" ]
    },
    "IpStack": {
      "ApiPrefix": "https://api.ipstack.com/",
      "ApiPostfix": "MovedToSecret"
    }
  }
  ```

After the above is done, you can just Dependency inject the `GeoIpService` in your C# class.

#### For example:


Then you have an endpoint that gives you info about the IP of the visitor.

```csharp
using GeoIpServices;
using Microsoft.AspNetCore.Mvc;
using MongoDbService;
using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddMongoDbServices();
builder.Services.AddGeoIpServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}


app.MapGet("/ipinfo", async ([FromServices] GeoIpService geoIpService, HttpRequest httpRequest) =>
{
	return await geoIpService.GetGeoIpInfoFromIpv4(GetOriginIpV4(httpRequest));
})
.WithName("GetGeoIpInfoFromIpv4")
.WithOpenApi();

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

### GitHub Repository
Visit our GitHub repository for the latest updates, documentation, and community contributions.
https://github.com/prmeyn/GeoIpServices


## License

This project is licensed under the GNU GENERAL PUBLIC LICENSE.

Happy coding! 🚀🌐📚



