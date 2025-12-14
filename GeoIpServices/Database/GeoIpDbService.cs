using GeoIpServices.Common;
using GeoIpServices.Database.DTOs;
using MongoDB.Driver;
using MongoDbService;
using System.Net;

namespace GeoIpServices.Database
{
	public sealed class GeoIpDbService
	{
		private readonly IMongoCollection<GeoIpInfoSession> _geoIpInfoSessionCollection;

		public GeoIpDbService(
			MongoService mongoService,
			GeoIpInitializer geoIpInitializer)
		{
			_geoIpInfoSessionCollection = mongoService.Database.GetCollection<GeoIpInfoSession>(nameof(GeoIpInfoSession), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

			// Create an index on IpV4
			var ipV4IndexKeys = Builders<GeoIpInfoSession>.IndexKeys.Ascending(x => x.IpV4);
			var ipV4IndexModel = new CreateIndexModel<GeoIpInfoSession>(ipV4IndexKeys);
			_ = _geoIpInfoSessionCollection.Indexes.CreateOneAsync(ipV4IndexModel);

			// Create TTL index on CreatedAtUTC - MongoDB will automatically delete expired documents
			var ttlIndexKeys = Builders<GeoIpInfoSession>.IndexKeys.Ascending(x => x.CreatedAtUTC);
			var ttlIndexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromSeconds(geoIpInitializer.GeoIpControls.SessionTimeoutInSeconds) };
			var ttlIndexModel = new CreateIndexModel<GeoIpInfoSession>(ttlIndexKeys, ttlIndexOptions);
			_ = _geoIpInfoSessionCollection.Indexes.CreateOneAsync(ttlIndexModel);
		}

		private FilterDefinition<GeoIpInfoSession> FilterByIpV4(IPAddress ipV4) => Builders<GeoIpInfoSession>.Filter.Eq(t => t.IpV4, ipV4.ToString());
		private FilterDefinition<GeoIpInfoSession> FilterBySessionId(string sessionId) => Builders<GeoIpInfoSession>.Filter.Eq(t => t.SessionId, sessionId);

		internal async Task<GeoIpInfoSession?> GetOrCreateAndGetLatestSession(IPAddress ipV4)
		{
			var latestSession = await GetLatestSession(ipV4);
			if (latestSession is not null)
			{
				return latestSession;
			}

			latestSession = new GeoIpInfoSession()
			{
				SessionId = Guid.NewGuid().ToString(),
				IpV4 = ipV4.ToString(),
				CreatedAtUTC = DateTime.UtcNow
			};

			await _geoIpInfoSessionCollection.InsertOneAsync(latestSession);

			return latestSession;
		}

		internal async Task UpdateSession(GeoIpInfoSession session)
		{
			var options = new ReplaceOptions { IsUpsert = true };
			await _geoIpInfoSessionCollection.ReplaceOneAsync(FilterBySessionId(session.SessionId), session, options);
		}

		internal async Task<GeoIpInfoSession?> GetLatestSession(IPAddress ipV4)
		{
			// MongoDB TTL index automatically removes expired documents, so we only need to find incomplete sessions
			var filter = Builders<GeoIpInfoSession>.Filter.And(
				FilterByIpV4(ipV4),
				Builders<GeoIpInfoSession>.Filter.Eq(x => x.IsCompleted, false)
			);

			return await _geoIpInfoSessionCollection
				.Find(filter)
				.SortByDescending(x => x.CreatedAtUTC)
				.FirstOrDefaultAsync();
		}
	}
}
