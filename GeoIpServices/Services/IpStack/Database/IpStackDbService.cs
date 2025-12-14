using GeoIpServices.Common;
using GeoIpServices.Services.IpStack.Database.DTOs;
using MongoDB.Driver;
using MongoDbService;

namespace GeoIpServices.Services.IpStack.Database
{
	public sealed class IpStackDbService
	{
		private readonly IMongoCollection<IpStackInfo> _ipStackInfoCollection;

		public IpStackDbService(
			MongoService mongoService,
			GeoIpInitializer geoIpInitializer)
		{
			_ipStackInfoCollection = mongoService.Database.GetCollection<IpStackInfo>(nameof(IpStackInfo), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

			// Create TTL index on ResponseTimeStampUTC - MongoDB will automatically delete expired cache entries
			var ttlIndexKeys = Builders<IpStackInfo>.IndexKeys.Ascending(x => x.ResponseTimeStampUTC);
			var ttlIndexOptions = new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(geoIpInitializer.GeoIpControls.CacheDurationInHours) };
			var ttlIndexModel = new CreateIndexModel<IpStackInfo>(ttlIndexKeys, ttlIndexOptions);
			_ = _ipStackInfoCollection.Indexes.CreateOneAsync(ttlIndexModel);
		}


		public async Task InsertOrOverwriteAsync(IpStackInfo responseValue)
		{
			var filter = Builders<IpStackInfo>.Filter.Eq(ip => ip.Id, responseValue.Id);
			var options = new ReplaceOptions { IsUpsert = true };
			await _ipStackInfoCollection.ReplaceOneAsync(filter, responseValue, options);
		}

		public async Task<IpStackInfo> GetByIdAsync(string ip)
		{
			var filter = Builders<IpStackInfo>.Filter.Eq(ipsi => ipsi.Id, ip);
			return await _ipStackInfoCollection.Find(filter).FirstOrDefaultAsync();
		}
	}
}
