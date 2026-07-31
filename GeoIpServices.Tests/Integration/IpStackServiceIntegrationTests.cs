using GeoIpServices.Common.DTOs;
using GeoIpServices.Services.IpStack;
using System.Net;

namespace GeoIpServices.Tests.Integration;

/// <summary>
/// Exercises the full provider path - cache read, upstream call, cache write - against a real MongoDB
/// server, with the HTTP side stubbed so no IpStack quota is spent.
/// </summary>
public sealed class IpStackServiceIntegrationTests
{
	private const string Ip = "1.2.3.4";

	private static string SuccessPayload(string ip = Ip) => $$"""
		{
		  "ip": "{{ip}}",
		  "country_code": "DK",
		  "country_name": "Denmark",
		  "city": "Copenhagen",
		  "location": { "languages": [ { "code": "da" } ], "is_eu": true }
		}
		""";

	private sealed class GatedHandler(string payload) : HttpMessageHandler
	{
		private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _callCount;

		public int CallCount => Volatile.Read(ref _callCount);
		public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

		public void Release() => _gate.TrySetResult();

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Interlocked.Increment(ref _callCount);
			Entered.TrySetResult();
			await _gate.Task;

			return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload) };
		}
	}

	private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
	{
		// The service disposes each client it creates, so the shared handler must survive that.
		public HttpClient CreateClient(string name) =>
			new(handler, disposeHandler: false) { BaseAddress = new Uri("https://api.ipstack.test/") };
	}

	[MongoFact]
	public async Task FirstLookup_CallsUpstreamAndCachesTheResult()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		GatedHandler handler = new(SuccessPayload());
		handler.Release();
		IpStackService service = scope.CreateIpStackService(new StubHttpClientFactory(handler));

		GeoIpInfo? result = await service.GetGeoIpInfoAsync(IPAddress.Parse(Ip), CancellationToken.None);

		Assert.NotNull(result);
		Assert.Equal("Copenhagen", result.City);
		Assert.Equal(1, handler.CallCount);
		Assert.Equal(1, await scope.RawIpStackInfoCollection.CountDocumentsAsync(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Empty));
	}

	[MongoFact]
	public async Task SecondLookup_IsServedFromTheCacheWithoutCallingUpstream()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		GatedHandler handler = new(SuccessPayload());
		handler.Release();
		IpStackService service = scope.CreateIpStackService(new StubHttpClientFactory(handler));

		await service.GetGeoIpInfoAsync(IPAddress.Parse(Ip), CancellationToken.None);
		GeoIpInfo? second = await service.GetGeoIpInfoAsync(IPAddress.Parse(Ip), CancellationToken.None);

		Assert.NotNull(second);
		Assert.Equal("Copenhagen", second.City);
		Assert.Equal(1, handler.CallCount);
	}

	/// <summary>
	/// The whole point of the cache is to spend one API call per address. Without coalescing, a burst of
	/// concurrent requests for an uncached address all miss and all pay.
	/// </summary>
	[MongoFact]
	public async Task ConcurrentLookupsOfTheSameAddress_ShareASingleUpstreamCall()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		GatedHandler handler = new(SuccessPayload());
		IpStackService service = scope.CreateIpStackService(new StubHttpClientFactory(handler));

		Task<GeoIpInfo?>[] lookups = [.. Enumerable.Range(0, 25)
			.Select(_ => service.GetGeoIpInfoAsync(IPAddress.Parse(Ip), CancellationToken.None))];

		// Hold the upstream call open until every caller has had a chance to join the in-flight lookup.
		await handler.Entered.Task;
		await Task.Delay(250);
		handler.Release();

		GeoIpInfo?[] results = await Task.WhenAll(lookups);

		Assert.Equal(1, handler.CallCount);
		Assert.All(results, result => Assert.Equal("Copenhagen", result?.City));
	}

	[MongoFact]
	public async Task DifferentAddresses_AreNotCoalescedTogether()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		GatedHandler handler = new(SuccessPayload());
		handler.Release();
		IpStackService service = scope.CreateIpStackService(new StubHttpClientFactory(handler));

		await service.GetGeoIpInfoAsync(IPAddress.Parse(Ip), CancellationToken.None);
		// The payload echoes 1.2.3.4, which will not match this address, so the response is rejected -
		// but it must still have been fetched rather than served from the other address's entry.
		await service.GetGeoIpInfoAsync(IPAddress.Parse("5.6.7.8"), CancellationToken.None);

		Assert.Equal(2, handler.CallCount);
	}

	[MongoFact]
	public async Task ErrorEnvelopeWithHttp200_IsTreatedAsAFailureAndNotCached()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		GatedHandler handler = new("""{"success":false,"error":{"code":101,"type":"invalid_access_key","info":"No API Key supplied."}}""");
		handler.Release();
		IpStackService service = scope.CreateIpStackService(new StubHttpClientFactory(handler));

		GeoIpInfo? result = await service.GetGeoIpInfoAsync(IPAddress.Parse(Ip), CancellationToken.None);

		Assert.Null(result);
		Assert.Equal(0, await scope.RawIpStackInfoCollection.CountDocumentsAsync(MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.Filter.Empty));
	}
}
