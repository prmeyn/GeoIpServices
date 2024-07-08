using System.Text.Json.Serialization;

namespace GeoIpServices.Services.IpStack.Database.DTOs
{
	public sealed class Language
	{
		[JsonPropertyName("code")] public string? Code { get; set; }
		[JsonPropertyName("name")] public string? Name { get; set; }
		[JsonPropertyName("native")] public string? Native { get; set; }
	}
}
