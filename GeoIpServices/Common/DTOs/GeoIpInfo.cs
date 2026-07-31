using EarthCountriesInfo;
using HumanLanguages;

namespace GeoIpServices.Common.DTOs
{
	/// <summary>
	/// The provider-independent geolocation result for an IP address.
	/// </summary>
	public sealed class GeoIpInfo
	{
		/// <summary>
		/// The languages spoken at the resolved location. Codes the provider returns that are not recognised
		/// are dropped rather than being reported as English.
		/// </summary>
		public HashSet<LanguageIsoCode>? LocationsLanguageIsoCodes { get; set; }

		/// <summary>The ISO country code, or <see langword="null"/> if the provider returned an unknown one.</summary>
		public CountryIsoCode? CountryCode { get; set; }

		/// <summary>The two-letter continent code, for example <c>EU</c>.</summary>
		public string? ContinentCode { get; set; }

		/// <summary>The continent name, for example <c>Europe</c>.</summary>
		public string? ContinentName { get; set; }

		/// <summary>The region or state code, for example <c>84</c> for Region Hovedstaden.</summary>
		public string? RegionCode { get; set; }

		/// <summary>The region or state name, for example <c>Region Hovedstaden</c>.</summary>
		public string? RegionName { get; set; }

		/// <summary>The city name, for example <c>Copenhagen</c>.</summary>
		public string? City { get; set; }

		/// <summary>The postal code.</summary>
		public string? Zip { get; set; }

		/// <summary>Approximate latitude of the location.</summary>
		public double? Latitude { get; set; }

		/// <summary>Approximate longitude of the location.</summary>
		public double? Longitude { get; set; }

		/// <summary>Whether the resolved country is an EU member state.</summary>
		public bool? IsEuMember { get; set; }
	}
}
