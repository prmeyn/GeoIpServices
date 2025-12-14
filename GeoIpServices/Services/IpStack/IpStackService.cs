using GeoIpServices.Common;
using GeoIpServices.Common.DTOs;
using GeoIpServices.Services.IpStack.Database;
using GeoIpServices.Services.IpStack.Database.DTOs;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace GeoIpServices.Services.IpStack
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

			string ip = ipV4.ToString();
			if (string.IsNullOrEmpty(ip))
			{
				return null;
			}

			try
			{
				// Check cache first - MongoDB TTL index handles expiration automatically
				IpStackInfo? cachedValue = await _ipStackDbService.GetByIdAsync(ip);
				if (cachedValue is not null)
				{
					return cachedValue.ToGeoIpInfo();
				}

				// Cache miss - fetch from API
				var response = await _httpClient.GetAsync($"{ip}{_ipStackInitializer.IpStackSettings.ApiPostfix}");
				response.EnsureSuccessStatusCode();

				string responseBody = await response.Content.ReadAsStringAsync();
				var responseValue = JsonSerializer.Deserialize<IpStackInfo>(responseBody);

				if (responseValue is not null && !string.IsNullOrEmpty(responseValue.Id) && responseValue.Id == ip && !string.IsNullOrWhiteSpace(responseValue.CountryCode))
				{
					responseValue.ResponseTimeStampUTC = DateTimeOffset.UtcNow;
					await _ipStackDbService.InsertOrOverwriteAsync(responseValue);
					return responseValue.ToGeoIpInfo();
				}

				_logger.LogWarning("IpStack returned invalid or incomplete response for IP: {IpAddress}", ip);
				return null;
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "HTTP request to IpStack failed for IP: {IpAddress}", ip);
				return null;
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "Failed to deserialize IpStack response for IP: {IpAddress}", ip);
				return null;
			}
			catch (TaskCanceledException ex)
			{
				_logger.LogWarning(ex, "IpStack request timed out for IP: {IpAddress}", ip);
				return null;
			}
		}
	}
}
