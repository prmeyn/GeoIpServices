using GeoIpStack.Database.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoDbService;

namespace GeoIpStack.Database
{
	public sealed class IpStackDbService
	{
		private readonly ILogger<IpStackDbService> _logger;
		IMongoDatabase _database;
		private IMongoCollection<IpStackInfo> _ipStackInfoCollection;
		private readonly string _apiPostfix;
		private readonly Uri _apiPrefixUri;

		public IpStackDbService(
			ILogger<IpStackDbService> logger,
			IHttpClientFactory httpClientFactory,
			IConfiguration configuration,
			MongoService mongoService)
		{
			_logger = logger;
			_ipStackInfoCollection = mongoService.Database.GetCollection<IpStackInfo>(nameof(IpStackInfo), new MongoCollectionSettings() { ReadConcern = ReadConcern.Majority, WriteConcern = WriteConcern.WMajority });

			// Retrieve the configuration values for ipStack section
			var ipStackConfig = configuration.GetSection("ipStack");

			_apiPrefixUri = new Uri(ipStackConfig["apiPrefix"]);
			_apiPostfix = ipStackConfig["apiPostfix"];
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

		internal Uri GetBaseAddress() => _apiPrefixUri;

		internal string GetApiPostfix() => _apiPostfix;
	}
}
