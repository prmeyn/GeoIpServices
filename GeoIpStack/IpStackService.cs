using GeoIpStack.Database;
using GeoIpStack.Database.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GeoIpStack
{
	public sealed class IpStackService
	{
		private readonly ILogger<IpStackService> _logger;
		private readonly HttpClient _httpClient;
		private readonly IpStackDbService _ipStackDbService;

		public IpStackService(
			ILogger<IpStackService> logger,
			IHttpClientFactory httpClientFactory,
			IpStackDbService ipStackDbService)
		{
			_logger = logger;
			_httpClient = httpClientFactory.CreateClient();
			_ipStackDbService = ipStackDbService;
			_httpClient.BaseAddress = _ipStackDbService.GetBaseAddress();
		}

		public async Task<IpStackInfo?> GetIpStackInfo(string ip)
		{
			try
			{
				if (!string.IsNullOrEmpty(ip))
				{
					IpStackInfo responseValue = await _ipStackDbService.GetByIdAsync(ip);
					if (responseValue != null && (DateTime.UtcNow - responseValue.ResponseTimeStampUTC).TotalHours < 24)
					{
						return responseValue;
					}
					var response = await _httpClient.GetAsync($"{ip}{_ipStackDbService.GetApiPostfix()}");
					response.EnsureSuccessStatusCode();
					string responseBody = await response.Content.ReadAsStringAsync();
					responseValue = JsonSerializer.Deserialize<IpStackInfo>(responseBody);
					if (responseValue != null && !string.IsNullOrEmpty(responseValue.Id) && responseValue.Id == ip && !string.IsNullOrWhiteSpace(responseValue.CountryCode))
					{
						responseValue.ResponseTimeStampUTC = DateTimeOffset.UtcNow;
						await _ipStackDbService.InsertOrOverwriteAsync(responseValue);
						return await Task.FromResult(responseValue);
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, ip);
			}
			return null;
		}
	}
}
