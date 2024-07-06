using GeoIpCommon;
using GeoIpServices.Database.DTOs;
using MongoDB.Driver;
using MongoDbService;
using System.Net;

namespace GeoIpServices.Database
{
	public sealed class GeoIpDbService
	{
		private IMongoCollection<GeoIpInfoSession> _geoIpInfoSessionCollection;
		private readonly GeoIpInitializer _geoIpInitializer;
		public GeoIpDbService(
			MongoService mongoService,
			GeoIpInitializer geoIpInitializer)
		{
			_geoIpInitializer = geoIpInitializer;
			_geoIpInfoSessionCollection = mongoService.Database.GetCollection<GeoIpInfoSession>(nameof(GeoIpInfoSession), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

			// Create an index on IpV4
			var indexKeys = Builders<GeoIpInfoSession>.IndexKeys.Ascending(x => x.IpV4);
			var indexModel = new CreateIndexModel<GeoIpInfoSession>(indexKeys);
			_ = _geoIpInfoSessionCollection.Indexes.CreateOneAsync(indexModel);
		}

		private FilterDefinition<GeoIpInfoSession> Filter(IPAddress ipV4) => Builders<GeoIpInfoSession>.Filter.Eq(t => t.IpV4, ipV4.ToString());
		private FilterDefinition<GeoIpInfoSession> Filter(string sessionId) => Builders<GeoIpInfoSession>.Filter.Eq(t => t.SessionId, sessionId);
		internal async Task<GeoIpInfoSession?> GetOrCreateAndGetLatestSession(IPAddress ipV4)
		{
			var latestSession = await GetLatestSession(ipV4);
			if (latestSession != null)
			{
				return latestSession;
			}
			latestSession = new GeoIpInfoSession()
			{
				SessionId = Guid.NewGuid().ToString(),
				IpV4 = ipV4.ToString(),
				StartTimeUTC = DateTimeOffset.UtcNow,
				ExpiryTimeUTC = DateTimeOffset.UtcNow.AddSeconds(_geoIpInitializer.GeoIpControls.SessionTimeoutInSeconds)
			};

			await _geoIpInfoSessionCollection.InsertOneAsync(latestSession);

			return latestSession;
		}

		internal async Task UpdateSession(GeoIpInfoSession session)
		{
			var options = new ReplaceOptions { IsUpsert = true };
			await _geoIpInfoSessionCollection.ReplaceOneAsync(Filter(session.SessionId), session, options);
		}

		internal async Task<GeoIpInfoSession?> GetLatestSession(IPAddress ipV4)
		{
			var allRecords = _geoIpInfoSessionCollection.Find(Filter(ipV4));

			if (allRecords?.Any() ?? false)
			{
				var list = allRecords.ToList();
				return list.Where(r => r.HasNotExpired())?
				.OrderByDescending(record => record.ExpiryTimeUTC)?
				.FirstOrDefault();
			}
			return null;
		}
	}
}
