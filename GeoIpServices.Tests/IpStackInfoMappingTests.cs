using EarthCountriesInfo;
using GeoIpServices.Common.DTOs;
using GeoIpServices.Services.IpStack.Database.DTOs;
using HumanLanguages;

namespace GeoIpServices.Tests;

public sealed class IpStackInfoMappingTests
{
	private static IpStackInfo WithLanguages(params string?[] languageCodes) => new()
	{
		Id = "1.2.3.4",
		CountryCode = "DK",
		Location = new Location
		{
			Languages = [.. languageCodes.Select(code => new Language { Code = code })]
		}
	};

	/// <summary>
	/// HumanHelper.CreateLanguageIsoCode falls back to English instead of failing, so an unrecognised code
	/// would reach the caller as a genuine-looking "en". The strict overload must be used instead.
	/// </summary>
	[Theory]
	[InlineData("zz")]
	[InlineData("not-a-real-language")]
	[InlineData("")]
	[InlineData(null)]
	public void UnknownLanguageCode_IsNotReportedAsEnglish(string? languageCode)
	{
		GeoIpInfo geoIpInfo = WithLanguages(languageCode).ToGeoIpInfo();

		Assert.DoesNotContain(
			geoIpInfo.LocationsLanguageIsoCodes ?? [],
			languageIsoCode => languageIsoCode.LanguageId == LanguageId.en);
	}

	[Fact]
	public void KnownLanguageCodes_AreMapped()
	{
		GeoIpInfo geoIpInfo = WithLanguages("da", "en").ToGeoIpInfo();

		Assert.NotNull(geoIpInfo.LocationsLanguageIsoCodes);
		Assert.Contains(geoIpInfo.LocationsLanguageIsoCodes, languageIsoCode => languageIsoCode.LanguageId == LanguageId.da);
		Assert.Contains(geoIpInfo.LocationsLanguageIsoCodes, languageIsoCode => languageIsoCode.LanguageId == LanguageId.en);
	}

	[Fact]
	public void KnownAndUnknownLanguageCodes_KeepOnlyTheKnownOnes()
	{
		GeoIpInfo geoIpInfo = WithLanguages("da", "zz").ToGeoIpInfo();

		Assert.NotNull(geoIpInfo.LocationsLanguageIsoCodes);
		Assert.Single(geoIpInfo.LocationsLanguageIsoCodes);
		Assert.Equal(LanguageId.da, geoIpInfo.LocationsLanguageIsoCodes.Single().LanguageId);
	}

	[Theory]
	[InlineData("DK", true)]
	[InlineData("dk", true)]
	[InlineData("ZZ", false)]
	[InlineData("", false)]
	[InlineData(null, false)]
	// Enum.TryParse also accepts numeric strings and yields undefined values, so these must not resolve.
	[InlineData("0", false)]
	[InlineData("999", false)]
	public void CountryCode_IsParsedStrictly(string? countryCode, bool expectResolved)
	{
		IpStackInfo ipStackInfo = new() { Id = "1.2.3.4", CountryCode = countryCode };

		CountryIsoCode? resolved = ipStackInfo.ToGeoIpInfo().CountryCode;

		Assert.Equal(expectResolved, resolved is not null);
		if (expectResolved)
		{
			Assert.Equal(CountryIsoCode.DK, resolved);
		}
	}

	[Fact]
	public void GeographicFields_AreSurfacedToTheCaller()
	{
		IpStackInfo ipStackInfo = new()
		{
			Id = "1.2.3.4",
			CountryCode = "DK",
			ContinentCode = "EU",
			ContinentName = "Europe",
			RegionCode = "84",
			RegionName = "Region Hovedstaden",
			City = "Copenhagen",
			Zip = "1050",
			Latitude = 55.6759,
			Longitude = 12.5655,
			Location = new Location { IsEU = true }
		};

		GeoIpInfo geoIpInfo = ipStackInfo.ToGeoIpInfo();

		Assert.Equal("EU", geoIpInfo.ContinentCode);
		Assert.Equal("Europe", geoIpInfo.ContinentName);
		Assert.Equal("84", geoIpInfo.RegionCode);
		Assert.Equal("Region Hovedstaden", geoIpInfo.RegionName);
		Assert.Equal("Copenhagen", geoIpInfo.City);
		Assert.Equal("1050", geoIpInfo.Zip);
		Assert.Equal(55.6759, geoIpInfo.Latitude);
		Assert.Equal(12.5655, geoIpInfo.Longitude);
		Assert.True(geoIpInfo.IsEuMember);
	}
}
