using EarthCountriesInfo;
using HumanLanguages;

namespace GeoIpServices.Common.DTOs
{
	public sealed class GeoIpInfo
	{
		public HashSet<LanguageIsoCode>? LocationsLanguageIsoCodes { get; set; }
		public CountryIsoCode? CountryCode { get; set; }
	}
}
