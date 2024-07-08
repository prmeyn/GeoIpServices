using EarthCountriesInfo;
using GeoIpServices.Common.DTOs;
using HumanLanguages;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace GeoIpServices.Services.IpStack.Database.DTOs
{
	public sealed class IpStackInfo
	{
		[BsonId]
		[JsonPropertyName("ip")] public string? Id { get; set; }
		[JsonPropertyName("type")] public string? Type { get; set; }
		[JsonPropertyName("continent_code")] public string? ContinentCode { get; set; }
		[JsonPropertyName("continent_name")] public string? ContinentName { get; set; }
		[JsonPropertyName("country_code")] public string? CountryCode { get; set; }
		[JsonPropertyName("country_name")] public string? CountryName { get; set; }
		[JsonPropertyName("region_code")] public string? RegionCode { get; set; }
		[JsonPropertyName("region_name")] public string? RegionName { get; set; }
		[JsonPropertyName("city")] public string? City { get; set; }
		[JsonPropertyName("zip")] public string? Zip { get; set; }
		[JsonPropertyName("latitude")] public double? Latitude { get; set; }
		[JsonPropertyName("longitude")] public double? Longitude { get; set; }
		[JsonPropertyName("location")] public Location? Location { get; set; }
		public DateTimeOffset ResponseTimeStampUTC { get; set; }

		internal GeoIpInfo ToGeoIpInfo()
		{
			return new GeoIpInfo() { 
				LocationsLanguageIsoCodes = Location?.Languages?.Select(languageCode => HumanHelper.CreateLanguageIsoCode(languageCode?.Code))?.ToHashSet(),
				CountryCode = Enum.TryParse(CountryCode, ignoreCase: true, out CountryIsoCode result) ? result : null
			};
		}
	}
}
