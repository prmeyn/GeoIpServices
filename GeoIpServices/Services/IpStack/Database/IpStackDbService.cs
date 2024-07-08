using GeoIpServices.Services.IpStack.Database.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbService;

namespace GeoIpServices.Services.IpStack.Database
{
	public sealed class IpStackDbService
	{
		private readonly ILogger<IpStackDbService> _logger;
		IMongoDatabase _database;
		private IMongoCollection<IpStackInfo> _ipStackInfoCollection;
		

		public IpStackDbService(
			ILogger<IpStackDbService> logger,
			IHttpClientFactory httpClientFactory,
			IConfiguration configuration,
			MongoService mongoService,
			IpStackInitializer ipStackInitializer)
		{
			_logger = logger;
			_ipStackInfoCollection = mongoService.Database.GetCollection<IpStackInfo>(nameof(IpStackInfo), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });
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
