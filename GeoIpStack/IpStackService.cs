using GeoIpCommon;
using GeoIpCommon.DTOs;
using GeoIpStack.Database;
using GeoIpStack.Database.DTOs;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace GeoIpStack
{
	public sealed class IpStackService : IGeoInfoService
	{
		private readonly ILogger<IpStackService> _logger;
		private readonly HttpClient _httpClient;
		private readonly IpStackDbService _ipStackDbService;
		private readonly IpStackInitializer _ipStackInitializer;

		public IpStackService(
			ILogger<IpStackService> logger,
			IHttpClientFactory httpClientFactory,
			IpStackDbService ipStackDbService,
			IpStackInitializer ipStackInitializer)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient();
			_ipStackDbService = ipStackDbService;
			_httpClient.BaseAddress = ipStackInitializer.IpStackSettings.ApiPrefix;
			_ipStackInitializer = ipStackInitializer;
		}

		public async Task<GeoIpInfo?> GetGeoIpInfoFromIpv4(IPAddress? ipV4)
		{
			if (ipV4 is null)
			{
				return null;
			}
			try
			{
				string ip = ipV4.ToString();
				if (!string.IsNullOrEmpty(ip))
				{
					IpStackInfo responseValue = await _ipStackDbService.GetByIdAsync(ip);
					if (responseValue != null && (DateTime.UtcNow - responseValue.ResponseTimeStampUTC).TotalHours < 24)
					{
						return await Task.FromResult(responseValue.ToGeoIpInfo());
					}
					var response = await _httpClient.GetAsync($"{ip}{_ipStackInitializer.IpStackSettings.ApiPostfix}");
					response.EnsureSuccessStatusCode();
					string responseBody = await response.Content.ReadAsStringAsync();
					responseValue = JsonSerializer.Deserialize<IpStackInfo>(responseBody);
					if (responseValue != null && !string.IsNullOrEmpty(responseValue.Id) && responseValue.Id == ip && !string.IsNullOrWhiteSpace(responseValue.CountryCode))
					{
						responseValue.ResponseTimeStampUTC = DateTimeOffset.UtcNow;
						await _ipStackDbService.InsertOrOverwriteAsync(responseValue);
						return await Task.FromResult(responseValue.ToGeoIpInfo());
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, ipV4?.ToString());
			}
			return null;
		}
	}
}
