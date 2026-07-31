using GeoIpServices.Services.IpStack.Database;
using GeoIpServices.Services.IpStack.Database.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace GeoIpServices.Tests.Integration;

/// <summary>
/// Exercises the cache against a real MongoDB server. These cover the behaviour that cannot be verified
/// in-process: what the server actually stores, and what it does with the TTL index.
/// </summary>
public sealed class IpStackDbServiceIntegrationTests
{
	private static IpStackInfo SampleEntry(string ip = "1.2.3.4") => new()
	{
		Id = ip,
		CountryCode = "DK",
		City = "Copenhagen",
		ResponseTimeStampUTC = DateTime.UtcNow
	};

	[MongoFact]
	public async Task EnsureIndexes_CreatesTheTtlIndexWithTheConfiguredDuration()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		IpStackDbService dbService = scope.CreateDbService(cacheDurationInHours: 24);

		await dbService.EnsureIndexesAsync(CancellationToken.None);

		Assert.Equal(24 * 3600d, await scope.GetExpireAfterSecondsAsync());
	}

	/// <summary>
	/// createIndex refuses to change expireAfterSeconds on an existing index and fails with error 85, so
	/// without the collMod fallback a changed CacheDurationInHours would silently keep the old TTL.
	/// </summary>
	[MongoFact]
	public async Task EnsureIndexes_UpdatesTheTtlWhenTheConfiguredDurationChanges()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();

		await scope.CreateDbService(cacheDurationInHours: 24).EnsureIndexesAsync(CancellationToken.None);
		Assert.Equal(24 * 3600d, await scope.GetExpireAfterSecondsAsync());

		await scope.CreateDbService(cacheDurationInHours: 48).EnsureIndexesAsync(CancellationToken.None);

		Assert.Equal(48 * 3600d, await scope.GetExpireAfterSecondsAsync());
	}

	[MongoFact]
	public async Task EnsureIndexes_IsIdempotent()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		IpStackDbService dbService = scope.CreateDbService();

		await dbService.EnsureIndexesAsync(CancellationToken.None);
		await dbService.EnsureIndexesAsync(CancellationToken.None);

		Assert.Equal(24 * 3600d, await scope.GetExpireAfterSecondsAsync());
	}

	/// <summary>
	/// The end-to-end form of the serialization guard: MongoDB's TTL monitor only deletes documents whose
	/// indexed field is a BSON date, so this is what makes cache expiry work at all.
	/// </summary>
	[MongoFact]
	public async Task StoredTimestamp_IsABsonDateOnTheServer()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();

		await scope.CreateDbService().InsertOrOverwriteAsync(SampleEntry(), CancellationToken.None);

		BsonDocument stored = await scope.RawIpStackInfoCollection
			.Find(Builders<BsonDocument>.Filter.Eq("_id", "1.2.3.4"))
			.FirstAsync();

		Assert.Equal(BsonType.DateTime, stored[nameof(IpStackInfo.ResponseTimeStampUTC)].BsonType);
	}

	[MongoFact]
	public async Task InsertAndGet_RoundTripsThroughTheServer()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		IpStackDbService dbService = scope.CreateDbService();
		IpStackInfo entry = SampleEntry();

		await dbService.InsertOrOverwriteAsync(entry, CancellationToken.None);
		IpStackInfo? retrieved = await dbService.GetByIdAsync("1.2.3.4", CancellationToken.None);

		Assert.NotNull(retrieved);
		Assert.Equal("DK", retrieved.CountryCode);
		Assert.Equal("Copenhagen", retrieved.City);
		// Round-tripped through BSON, which stores milliseconds rather than ticks.
		Assert.Equal(entry.ResponseTimeStampUTC, retrieved.ResponseTimeStampUTC, TimeSpan.FromMilliseconds(1));
	}

	[MongoFact]
	public async Task GetById_ReturnsNullOnAMiss()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();

		Assert.Null(await scope.CreateDbService().GetByIdAsync("203.0.113.9", CancellationToken.None));
	}

	[MongoFact]
	public async Task InsertOrOverwrite_ReplacesAnExistingEntry()
	{
		await using MongoTestScope scope = await MongoTestScope.CreateAsync();
		IpStackDbService dbService = scope.CreateDbService();

		await dbService.InsertOrOverwriteAsync(SampleEntry(), CancellationToken.None);

		IpStackInfo updated = SampleEntry();
		updated.City = "Aarhus";
		await dbService.InsertOrOverwriteAsync(updated, CancellationToken.None);

		IpStackInfo? retrieved = await dbService.GetByIdAsync("1.2.3.4", CancellationToken.None);
		Assert.Equal("Aarhus", retrieved?.City);
		Assert.Equal(1, await scope.RawIpStackInfoCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty));
	}
}
