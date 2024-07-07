using EarthCountriesInfo;
using HumanLanguages;

namespace GeoIpCommon.DTOs
{
	public sealed class GeoIpInfo
	{
		public HashSet<LanguageIsoCode>? LocationsLanguageIsoCodes { get; set; }
		public CountryIsoCode? CountryCode { get; set; }
	}
}
