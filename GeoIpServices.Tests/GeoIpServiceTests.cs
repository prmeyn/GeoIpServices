using GeoIpServices.Common;
using GeoIpServices.Common.DTOs;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace GeoIpServices.Tests;

public sealed class GeoIpServiceTests
{
	private sealed class StubProvider(GeoIpInfo? result) : IGeoIpProvider
	{
		public int CallCount { get; private set; }
		public List<IPAddress> ReceivedAddresses { get; } = [];

		public GeoIpInfoProvider Provider => GeoIpInfoProvider.IpStack;

		public Task<GeoIpInfo?> GetGeoIpInfoAsync(IPAddress ipAddress, CancellationToken cancellationToken)
		{
			CallCount++;
			ReceivedAddresses.Add(ipAddress);
			return Task.FromResult(result);
		}
	}

	private static GeoIpService CreateService(StubProvider provider, byte maxRoundRobinAttempts = 1)
	{
		Dictionary<string, string?> configuration = TestConfiguration.Valid();
		configuration["GeoIpSettings:Controls:MaxRoundRobinAttempts"] = maxRoundRobinAttempts.ToString();

		return new GeoIpService(
			new GeoIpInitializer(TestConfiguration.Build(configuration)),
			[provider],
			NullLogger<GeoIpService>.Instance);
	}

	/// <summary>
	/// MapToIPv4() does not validate, so a native IPv6 address would be reinterpreted as a bogus IPv4 one
	/// (2001:db8::1 becomes 0.0.0.1), spending API quota on a meaningless lookup and caching the result.
	/// </summary>
	[Theory]
	[InlineData("2001:db8::1")]
	[InlineData("2a03:2880:f003:c07:face:b00c::2")]
	[InlineData("::1")]
	public async Task NativeIPv6_IsRejectedWithoutCallingAnyProvider(string ipAddress)
	{
		StubProvider provider = new(new GeoIpInfo());
		GeoIpService service = CreateService(provider);

		GeoIpInfo? result = await service.GetGeoIpInfoFromIpv4(IPAddress.Parse(ipAddress));

		Assert.Null(result);
		Assert.Equal(0, provider.CallCount);
	}

	[Fact]
	public async Task IPv4MappedIPv6_IsUnwrappedToIPv4()
	{
		StubProvider provider = new(new GeoIpInfo());
		GeoIpService service = CreateService(provider);

		await service.GetGeoIpInfoFromIpv4(IPAddress.Parse("::ffff:1.2.3.4"));

		Assert.Equal(IPAddress.Parse("1.2.3.4"), Assert.Single(provider.ReceivedAddresses));
	}

	[Fact]
	public async Task PlainIPv4_IsPassedThroughUnchanged()
	{
		StubProvider provider = new(new GeoIpInfo());
		GeoIpService service = CreateService(provider);

		await service.GetGeoIpInfoFromIpv4(IPAddress.Parse("1.2.3.4"));

		Assert.Equal(IPAddress.Parse("1.2.3.4"), Assert.Single(provider.ReceivedAddresses));
	}

	[Fact]
	public async Task NullAddress_ReturnsNull()
	{
		StubProvider provider = new(new GeoIpInfo());
		GeoIpService service = CreateService(provider);

		Assert.Null(await service.GetGeoIpInfoFromIpv4(null));
		Assert.Equal(0, provider.CallCount);
	}

	[Fact]
	public async Task SuccessfulLookup_ReturnsTheProviderResult()
	{
		GeoIpInfo expected = new() { City = "Copenhagen" };
		StubProvider provider = new(expected);
		GeoIpService service = CreateService(provider);

		Assert.Same(expected, await service.GetGeoIpInfoFromIpv4(IPAddress.Parse("1.2.3.4")));
		Assert.Equal(1, provider.CallCount);
	}

	/// <summary>
	/// The retry budget is per call and bounded. Previously a persistently failing provider was retried the
	/// full budget on every inbound request forever, because the queue tracking it was never persisted.
	/// </summary>
	[Theory]
	[InlineData((byte)1)]
	[InlineData((byte)3)]
	public async Task FailingProvider_IsCalledExactlyMaxRoundRobinAttemptsTimes(byte maxRoundRobinAttempts)
	{
		StubProvider provider = new(null);
		GeoIpService service = CreateService(provider, maxRoundRobinAttempts);

		Assert.Null(await service.GetGeoIpInfoFromIpv4(IPAddress.Parse("1.2.3.4")));

		Assert.Equal(maxRoundRobinAttempts, provider.CallCount);
	}

	[Fact]
	public void PriorityNamingAnUnregisteredProvider_FailsFast()
	{
		GeoIpInitializer initializer = new(TestConfiguration.Build(TestConfiguration.Valid()));

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => new GeoIpService(initializer, [], NullLogger<GeoIpService>.Instance));

		Assert.Contains("IpStack", exception.Message, StringComparison.Ordinal);
	}

	[Fact]
	public async Task CancelledToken_StopsTheLookup()
	{
		StubProvider provider = new(null);
		GeoIpService service = CreateService(provider);
		using CancellationTokenSource cancellationTokenSource = new();
		await cancellationTokenSource.CancelAsync();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => service.GetGeoIpInfoFromIpv4(IPAddress.Parse("1.2.3.4"), cancellationTokenSource.Token));

		Assert.Equal(0, provider.CallCount);
	}
}
