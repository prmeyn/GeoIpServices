using EarthCountriesInfo;
using GeoIpServices.Common;
using GeoIpServices.Common.DTOs;
using HumanLanguages;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace GeoIpServices.Services.IpStack.Database.DTOs
{
	/// <summary>
	/// A cached IpStack response. Doubles as the JSON wire model and the MongoDB storage model.
	/// </summary>
	internal sealed class IpStackInfo
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

		/// <summary>
		/// When this entry was cached. Drives the TTL index, so the type matters: the MongoDB driver
		/// serializes <see cref="DateTimeOffset"/> as a sub-document, and MongoDB's TTL monitor only ever
		/// deletes documents whose indexed field is a BSON date. Stored as <see cref="DateTime"/> in UTC so
		/// expiry actually happens.
		/// </summary>
		[JsonIgnore] public DateTime ResponseTimeStampUTC { get; set; }

		internal GeoIpInfo ToGeoIpInfo()
		{
			return new GeoIpInfo()
			{
				LocationsLanguageIsoCodes = MapLanguageIsoCodes(),
				CountryCode = EnumParser.TryParseName(CountryCode, out CountryIsoCode countryIsoCode) ? countryIsoCode : null,
				ContinentCode = ContinentCode,
				ContinentName = ContinentName,
				RegionCode = RegionCode,
				RegionName = RegionName,
				City = City,
				Zip = Zip,
				Latitude = Latitude,
				Longitude = Longitude,
				IsEuMember = Location?.IsEU
			};
		}

		private HashSet<LanguageIsoCode>? MapLanguageIsoCodes()
		{
			Language[]? languages = Location?.Languages;
			if (languages is null || languages.Length == 0)
			{
				return null;
			}

			HashSet<LanguageIsoCode>? languageIsoCodes = null;
			foreach (Language? language in languages)
			{
				// HumanHelper.CreateLanguageIsoCode never fails - it falls back to English - so an
				// unrecognised code would reach the caller as an ordinary "en" result, indistinguishable
				// from a genuine one. TryCreateLanguageIsoCode is strict, so unknown codes are dropped.
				if (HumanHelper.TryCreateLanguageIsoCode(language?.Code ?? string.Empty, out LanguageIsoCode? languageIsoCode)
					&& languageIsoCode is not null)
				{
					(languageIsoCodes ??= []).Add(languageIsoCode);
				}
			}

			return languageIsoCodes;
		}
	}
}
