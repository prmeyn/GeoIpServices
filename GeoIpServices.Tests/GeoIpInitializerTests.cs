using GeoIpServices.Common;

namespace GeoIpServices.Tests;

public sealed class GeoIpInitializerTests
{
	private static GeoIpInitializer Create(Action<Dictionary<string, string?>> configure)
	{
		Dictionary<string, string?> configuration = TestConfiguration.Valid();
		configure(configuration);
		return new GeoIpInitializer(TestConfiguration.Build(configuration));
	}

	[Fact]
	public void ValidConfiguration_IsAccepted()
	{
		GeoIpControls controls = Create(_ => { }).GeoIpControls;

		Assert.Equal(24, controls.CacheDurationInHours);
		Assert.Equal(1, controls.MaxRoundRobinAttempts);
		Assert.Equal([GeoIpInfoProvider.IpStack], controls.Priority);
	}

	[Fact]
	public void MissingOptionalValues_FallBackToDefaults()
	{
		GeoIpControls controls = Create(configuration =>
		{
			configuration.Remove("GeoIpSettings:Controls:MaxRoundRobinAttempts");
			configuration.Remove("GeoIpSettings:Controls:CacheDurationInHours");
		}).GeoIpControls;

		Assert.Equal(1, controls.MaxRoundRobinAttempts);
		Assert.Equal(24, controls.CacheDurationInHours);
	}

	[Fact]
	public void MissingPriority_Throws()
	{
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration.Remove("GeoIpSettings:Controls:Priority:0")));

		Assert.Contains("Priority", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>An unrecognised name must be reported, not silently dropped.</summary>
	[Fact]
	public void UnknownProviderName_ThrowsAndNamesTheOffendingValue()
	{
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:Controls:Priority:0"] = "IpStck"));

		Assert.Contains("IpStck", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>Enum.TryParse accepts numeric strings, so "0" would otherwise resolve to the first provider.</summary>
	[Theory]
	[InlineData("0")]
	[InlineData("999")]
	[InlineData("-1")]
	public void NumericProviderName_IsRejected(string configuredName)
	{
		Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:Controls:Priority:0"] = configuredName));
	}

	[Fact]
	public void DuplicateProviders_AreCollapsedButOrderIsPreserved()
	{
		GeoIpControls controls = Create(configuration =>
		{
			configuration["GeoIpSettings:Controls:Priority:1"] = "IpStack";
		}).GeoIpControls;

		Assert.Equal([GeoIpInfoProvider.IpStack], controls.Priority);
	}

	/// <summary>Zero would drain the retry budget before any provider was called.</summary>
	[Theory]
	[InlineData("0")]
	[InlineData("-1")]
	[InlineData("not-a-number")]
	[InlineData("999")]
	public void InvalidMaxRoundRobinAttempts_Throws(string configuredValue)
	{
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:Controls:MaxRoundRobinAttempts"] = configuredValue));

		Assert.Contains("MaxRoundRobinAttempts", exception.Message, StringComparison.Ordinal);
	}

	/// <summary>A non-positive duration yields a TTL index that expires entries the moment they are written.</summary>
	[Theory]
	[InlineData("0")]
	[InlineData("-5")]
	[InlineData("not-a-number")]
	public void InvalidCacheDuration_Throws(string configuredValue)
	{
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:Controls:CacheDurationInHours"] = configuredValue));

		Assert.Contains("CacheDurationInHours", exception.Message, StringComparison.Ordinal);
	}
}
