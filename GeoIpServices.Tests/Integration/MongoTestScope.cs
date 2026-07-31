using GeoIpServices.Common;
using GeoIpServices.Services.IpStack;
using GeoIpServices.Services.IpStack.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbService;

namespace GeoIpServices.Tests.Integration;

/// <summary>
/// An isolated, uniquely named database for one test, dropped when the scope is disposed. Nothing outside
/// the scope's own database is ever touched.
/// </summary>
internal sealed class MongoTestScope : IAsyncDisposable
{
	private MongoTestScope(string databaseName, MongoService mongoService)
	{
		DatabaseName = databaseName;
		MongoService = mongoService;
	}

	internal string DatabaseName { get; }
	internal MongoService MongoService { get; }
	internal IMongoDatabase Database => MongoService.Database;

	internal IMongoCollection<BsonDocument> RawIpStackInfoCollection =>
		Database.GetCollection<BsonDocument>("IpStackInfo");

	internal static async Task<MongoTestScope> CreateAsync()
	{
		string databaseName = $"GeoIpServices_IntegrationTests_{Guid.NewGuid():N}";

		MongoService mongoService = new(BuildConfiguration(databaseName, cacheDurationInHours: 24), NullLogger<MongoService>.Instance);

		// The service records a connection document in the background; letting it finish keeps the drop
		// in DisposeAsync from racing against it.
		await mongoService.ConnectionRecorded;

		return new MongoTestScope(databaseName, mongoService);
	}

	private static IConfiguration BuildConfiguration(string databaseName, int cacheDurationInHours)
	{
		Dictionary<string, string?> settings = TestConfiguration.Valid();
		settings["MongoDbSettings:ConnectionString"] = MongoTestEnvironment.ConnectionString;
		settings["MongoDbSettings:DatabaseName"] = databaseName;
		settings["GeoIpSettings:Controls:CacheDurationInHours"] = cacheDurationInHours.ToString();
		return TestConfiguration.Build(settings);
	}

	/// <summary>Builds a cache service over this scope's database with the given cache duration.</summary>
	internal IpStackDbService CreateDbService(int cacheDurationInHours = 24) =>
		new(MongoService,
			new GeoIpInitializer(BuildConfiguration(DatabaseName, cacheDurationInHours)),
			NullLogger<IpStackDbService>.Instance);

	internal IpStackService CreateIpStackService(IHttpClientFactory httpClientFactory, int cacheDurationInHours = 24) =>
		new(NullLogger<IpStackService>.Instance,
			httpClientFactory,
			CreateDbService(cacheDurationInHours),
			new IpStackInitializer(BuildConfiguration(DatabaseName, cacheDurationInHours)));

	/// <summary>Reads the TTL setting the server actually holds for the cache index.</summary>
	internal async Task<double?> GetExpireAfterSecondsAsync()
	{
		List<BsonDocument> indexes = await RawIpStackInfoCollection.Indexes.List().ToListAsync();

		BsonDocument? ttlIndex = indexes.SingleOrDefault(index =>
			index["key"].AsBsonDocument.Contains(nameof(Services.IpStack.Database.DTOs.IpStackInfo.ResponseTimeStampUTC)));

		return ttlIndex is not null && ttlIndex.Contains("expireAfterSeconds")
			? ttlIndex["expireAfterSeconds"].ToDouble()
			: null;
	}

	public async ValueTask DisposeAsync() => await Database.Client.DropDatabaseAsync(DatabaseName);
}
