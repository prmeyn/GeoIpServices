using Microsoft.Extensions.Configuration;

namespace GeoIpServices.Tests;

internal static class TestConfiguration
{
	internal static IConfiguration Build(Dictionary<string, string?> values) =>
		new ConfigurationBuilder().AddInMemoryCollection(values).Build();

	/// <summary>A complete, valid configuration, so each test can override only the key it is about.</summary>
	internal static Dictionary<string, string?> Valid() => new()
	{
		["GeoIpSettings:Controls:MaxRoundRobinAttempts"] = "1",
		["GeoIpSettings:Controls:CacheDurationInHours"] = "24",
		["GeoIpSettings:Controls:Priority:0"] = "IpStack",
		["GeoIpSettings:IpStack:ApiPrefix"] = "https://api.ipstack.com/",
		["GeoIpSettings:IpStack:ApiPostfix"] = "?access_key=TEST_ACCESS_KEY"
	};
}
