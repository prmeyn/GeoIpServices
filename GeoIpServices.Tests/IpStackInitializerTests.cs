using GeoIpServices.Services.IpStack;

namespace GeoIpServices.Tests;

public sealed class IpStackInitializerTests
{
	private static IpStackInitializer Create(Action<Dictionary<string, string?>> configure)
	{
		Dictionary<string, string?> configuration = TestConfiguration.Valid();
		configure(configuration);
		return new IpStackInitializer(TestConfiguration.Build(configuration));
	}

	[Fact]
	public void ValidConfiguration_IsAccepted()
	{
		IpStackSettings settings = Create(_ => { }).IpStackSettings;

		Assert.Equal(new Uri("https://api.ipstack.com/"), settings.ApiPrefix);
		Assert.Equal("?access_key=TEST_ACCESS_KEY", settings.ApiPostfix);
		Assert.Equal(TimeSpan.FromSeconds(10), settings.RequestTimeout);
	}

	/// <summary>
	/// A base address whose path has no trailing slash loses its last segment when a relative URI is
	/// resolved against it, so ".../v1" would quietly request ".../1.2.3.4".
	/// </summary>
	[Theory]
	[InlineData("https://api.ipstack.com", "https://api.ipstack.com/")]
	[InlineData("https://api.ipstack.com/", "https://api.ipstack.com/")]
	[InlineData("https://api.ipstack.com/v1", "https://api.ipstack.com/v1/")]
	[InlineData("https://api.ipstack.com/v1/", "https://api.ipstack.com/v1/")]
	public void ApiPrefix_AlwaysEndsInASlash(string configuredValue, string expected)
	{
		IpStackSettings settings = Create(configuration => configuration["GeoIpSettings:IpStack:ApiPrefix"] = configuredValue).IpStackSettings;

		Assert.Equal(new Uri(expected), settings.ApiPrefix);
	}

	[Theory]
	[InlineData("")]
	[InlineData("not a uri")]
	// On Unix this parses as an absolute file path (file:///relative/only) and on Windows it does not,
	// so only a scheme check rejects it consistently on both.
	[InlineData("/relative/only")]
	[InlineData("file:///tmp/whatever")]
	[InlineData("ftp://api.ipstack.com/")]
	[InlineData("https://api.ipstack.com/?access_key=LEAKED")]
	public void InvalidApiPrefix_Throws(string configuredValue)
	{
		Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:IpStack:ApiPrefix"] = configuredValue));
	}

	[Theory]
	[InlineData("")]
	[InlineData("access_key=NO_QUESTION_MARK")]
	[InlineData("?apikey=WRONG_PARAMETER")]
	[InlineData("?access_key=")]
	public void InvalidApiPostfix_Throws(string configuredValue)
	{
		Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:IpStack:ApiPostfix"] = configuredValue));
	}

	/// <summary>Validation messages must never echo the configured value - it carries the access key.</summary>
	[Fact]
	public void ApiPostfixValidationError_DoesNotLeakTheAccessKey()
	{
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:IpStack:ApiPostfix"] = "access_key=SUPER_SECRET_KEY"));

		Assert.DoesNotContain("SUPER_SECRET_KEY", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public void MissingTimeout_FallsBackToTheDefault()
	{
		IpStackSettings settings = Create(configuration => configuration.Remove("GeoIpSettings:IpStack:TimeoutInSeconds")).IpStackSettings;

		Assert.Equal(TimeSpan.FromSeconds(10), settings.RequestTimeout);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("-1")]
	[InlineData("not-a-number")]
	public void InvalidTimeout_Throws(string configuredValue)
	{
		Assert.Throws<InvalidOperationException>(
			() => Create(configuration => configuration["GeoIpSettings:IpStack:TimeoutInSeconds"] = configuredValue));
	}
}
