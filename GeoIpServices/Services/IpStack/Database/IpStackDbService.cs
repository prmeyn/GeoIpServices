using GeoIpServices.Common;
using GeoIpServices.Services.IpStack.Database.DTOs;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDbService;

namespace GeoIpServices.Services.IpStack.Database
{
	/// <summary>
	/// The MongoDB-backed IpStack response cache.
	/// </summary>
	internal sealed class IpStackDbService
	{
		/// <summary>MongoDB's <c>IndexOptionsConflict</c>: same keys, different options.</summary>
		private const int IndexOptionsConflictErrorCode = 85;

		private readonly IMongoDatabase _database;
		private readonly IMongoCollection<IpStackInfo> _ipStackInfoCollection;
		private readonly GeoIpInitializer _geoIpInitializer;
		private readonly ILogger<IpStackDbService> _logger;

		public IpStackDbService(
			MongoService mongoService,
			GeoIpInitializer geoIpInitializer,
			ILogger<IpStackDbService> logger)
		{
			ArgumentNullException.ThrowIfNull(mongoService);

			_database = mongoService.Database;
			_ipStackInfoCollection = _database.GetCollection<IpStackInfo>(
				nameof(IpStackInfo),
				new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });
			_geoIpInitializer = geoIpInitializer;
			_logger = logger;
		}

		/// <summary>
		/// Creates the TTL index that expires cached entries, or brings an existing one in line with the
		/// configured duration.
		/// </summary>
		/// <remarks>
		/// Awaited from startup rather than fired and forgotten in the constructor: a discarded task hides
		/// connection and permission failures completely, leaving the collection unindexed and never
		/// expiring while the application still reports healthy.
		/// </remarks>
		internal async Task EnsureIndexesAsync(CancellationToken cancellationToken)
		{
			TimeSpan expireAfter = TimeSpan.FromHours(_geoIpInitializer.GeoIpControls.CacheDurationInHours);

			CreateIndexModel<IpStackInfo> ttlIndexModel = new(
				Builders<IpStackInfo>.IndexKeys.Ascending(ipStackInfo => ipStackInfo.ResponseTimeStampUTC),
				new CreateIndexOptions { ExpireAfter = expireAfter });

			try
			{
				await _ipStackInfoCollection.Indexes
					.CreateOneAsync(ttlIndexModel, cancellationToken: cancellationToken)
					.ConfigureAwait(false);
			}
			catch (MongoCommandException ex) when (ex.Code == IndexOptionsConflictErrorCode)
			{
				// createIndex refuses to change expireAfterSeconds on an existing index. Without this,
				// changing CacheDurationInHours would appear to work while the old TTL stayed in force.
				await UpdateTimeToLiveAsync(expireAfter, cancellationToken).ConfigureAwait(false);
			}
		}

		private async Task UpdateTimeToLiveAsync(TimeSpan expireAfter, CancellationToken cancellationToken)
		{
			long expireAfterSeconds = (long)expireAfter.TotalSeconds;

			// Identified by key pattern rather than by name, so this works against indexes created earlier
			// under MongoDB's default naming.
			BsonDocument collModCommand = new()
			{
				{ "collMod", _ipStackInfoCollection.CollectionNamespace.CollectionName },
				{
					"index", new BsonDocument
					{
						{ "keyPattern", new BsonDocument { { nameof(IpStackInfo.ResponseTimeStampUTC), 1 } } },
						{ "expireAfterSeconds", expireAfterSeconds }
					}
				}
			};

			await _database.RunCommandAsync<BsonDocument>(collModCommand, cancellationToken: cancellationToken).ConfigureAwait(false);

			_logger.LogInformation(
				"Updated the {Collection} TTL index to expire entries after {ExpireAfterSeconds}s.",
				_ipStackInfoCollection.CollectionNamespace.CollectionName,
				expireAfterSeconds);
		}

		internal async Task InsertOrOverwriteAsync(IpStackInfo responseValue, CancellationToken cancellationToken)
		{
			FilterDefinition<IpStackInfo> filter = Builders<IpStackInfo>.Filter.Eq(ipStackInfo => ipStackInfo.Id, responseValue.Id);

			await _ipStackInfoCollection
				.ReplaceOneAsync(filter, responseValue, new ReplaceOptions { IsUpsert = true }, cancellationToken)
				.ConfigureAwait(false);
		}

		internal async Task<IpStackInfo?> GetByIdAsync(string ip, CancellationToken cancellationToken)
		{
			FilterDefinition<IpStackInfo> filter = Builders<IpStackInfo>.Filter.Eq(ipStackInfo => ipStackInfo.Id, ip);

			return await _ipStackInfoCollection
				.Find(filter)
				.FirstOrDefaultAsync(cancellationToken)
				.ConfigureAwait(false);
		}
	}
}
