using GeoIpServices.Database.DTOs;
using MongoDB.Driver;
using MongoDbService;
using System.Net;

namespace GeoIpServices.Database
{
	public sealed class GeoIpDbService
	{
		private IMongoCollection<GeoIpInfoSession> _geoIpInfoSessionCollection;
		public GeoIpDbService(MongoService mongoService)
		{
			_geoIpInfoSessionCollection = mongoService.Database.GetCollection<GeoIpInfoSession>(nameof(GeoIpInfoSession), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

			// Create an index on IpV4
			var indexKeys = Builders<GeoIpInfoSession>.IndexKeys.Ascending(x => x.IpV4);
			var indexModel = new CreateIndexModel<GeoIpInfoSession>(indexKeys);
			_ = _geoIpInfoSessionCollection.Indexes.CreateOneAsync(indexModel);
		}

		internal Task<GeoIpInfoSession?> GetOrCreateAndGetLatestSession(IPAddress ipV4)
		{
			throw new NotImplementedException();
		}

		internal Task UpdateSession(GeoIpInfoSession session)
		{
			throw new NotImplementedException();
		}
	}
}
