using GeoIpServices.Common;
using GeoIpServices.Common.DTOs;
using GeoIpServices.Services.IpStack.Database;
using GeoIpServices.Services.IpStack.Database.DTOs;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace GeoIpServices.Services.IpStack
{
	/// <summary>
	/// The IpStack provider: serves from the MongoDB cache when it can, and calls the API when it cannot.
	/// </summary>
	internal sealed class IpStackService : IGeoIpProvider
	{
		internal const string HttpClientName = "GeoIpServices.IpStack";

		private readonly ILogger<IpStackService> _logger;
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly IpStackDbService _ipStackDbService;
		private readonly IpStackInitializer _ipStackInitializer;

		/// <summary>
		/// Collapses concurrent lookups of the same address into one upstream call. Without it, N
		/// simultaneous requests for an uncached IP all miss the cache and all spend quota on the same
		/// lookup - which is exactly the traffic pattern the cache exists to protect against.
		/// </summary>
		private readonly ConcurrentDictionary<string, Lazy<Task<GeoIpInfo?>>> _inFlightLookups = new(StringComparer.Ordinal);

		public IpStackService(
			ILogger<IpStackService> logger,
			IHttpClientFactory httpClientFactory,
			IpStackDbService ipStackDbService,
			IpStackInitializer ipStackInitializer)
		{
			_logger = logger;
			_httpClientFactory = httpClientFactory;
			_ipStackDbService = ipStackDbService;
			_ipStackInitializer = ipStackInitializer;
		}

		public GeoIpInfoProvider Provider => GeoIpInfoProvider.IpStack;

		public async Task<GeoIpInfo?> GetGeoIpInfoAsync(IPAddress ipAddress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(ipAddress);

			string ip = ipAddress.ToString();

			Lazy<Task<GeoIpInfo?>>? startedByThisCaller = null;
			Lazy<Task<GeoIpInfo?>> lookup = _inFlightLookups.GetOrAdd(
				ip,
				_ => startedByThisCaller = new Lazy<Task<GeoIpInfo?>>(() => LookupAsync(ip)));

			if (ReferenceEquals(lookup, startedByThisCaller))
			{
				// Only the caller that started the lookup retires it, and only once it has actually
				// finished, so a caller that cancels early cannot evict an entry others are still awaiting.
				_ = lookup.Value.ContinueWith(
					_ => _inFlightLookups.TryRemove(new KeyValuePair<string, Lazy<Task<GeoIpInfo?>>>(ip, lookup)),
					CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously,
					TaskScheduler.Default);
			}

			// The shared task deliberately does not observe any individual caller's token: one caller
			// cancelling must not fail the lookup for everyone else waiting on the same result.
			return await lookup.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
		}

		private async Task<GeoIpInfo?> LookupAsync(string ip)
		{
			try
			{
				IpStackInfo? cachedValue = await _ipStackDbService.GetByIdAsync(ip, CancellationToken.None).ConfigureAwait(false);
				if (cachedValue is not null)
				{
					return cachedValue.ToGeoIpInfo();
				}

				string responseBody = await FetchAsync(ip).ConfigureAwait(false);

				if (TryReadError(responseBody, out IpStackError? error))
				{
					_logger.LogError(
						"IpStack rejected the request for IP {IpAddress}: error {ErrorCode} ({ErrorType}). {ErrorInfo}",
						ip, error?.Code, error?.Type, error?.Info);
					return null;
				}

				IpStackInfo? responseValue = JsonSerializer.Deserialize<IpStackInfo>(responseBody);
				if (responseValue is null
					|| string.IsNullOrEmpty(responseValue.Id)
					|| !string.Equals(responseValue.Id, ip, StringComparison.Ordinal)
					|| string.IsNullOrWhiteSpace(responseValue.CountryCode))
				{
					_logger.LogWarning("IpStack returned an invalid or incomplete response for IP: {IpAddress}", ip);
					return null;
				}

				responseValue.ResponseTimeStampUTC = DateTime.UtcNow;
				await _ipStackDbService.InsertOrOverwriteAsync(responseValue, CancellationToken.None).ConfigureAwait(false);

				return responseValue.ToGeoIpInfo();
			}
			catch (HttpRequestException ex)
			{
				_logger.LogError(ex, "HTTP request to IpStack failed for IP: {IpAddress}", ip);
				return null;
			}
			catch (TaskCanceledException ex)
			{
				_logger.LogWarning(ex, "IpStack request timed out for IP: {IpAddress}", ip);
				return null;
			}
			catch (JsonException ex)
			{
				_logger.LogError(ex, "Failed to deserialize the IpStack response for IP: {IpAddress}", ip);
				return null;
			}
			catch (MongoException ex)
			{
				_logger.LogError(ex, "Database error while caching geo info for IP: {IpAddress}", ip);
				return null;
			}
			catch (TimeoutException ex)
			{
				// The driver surfaces operation timeouts as System.TimeoutException, which derives from
				// neither MongoException nor anything else caught above - so without this a slow database
				// would escape as an unhandled exception rather than degrading to a cache miss.
				_logger.LogError(ex, "Database operation timed out while looking up IP: {IpAddress}", ip);
				return null;
			}
		}

		private async Task<string> FetchAsync(string ip)
		{
			// Resolved per request so the factory can rotate handlers; a client held for the lifetime of
			// this singleton would pin the connection pool to a stale DNS result.
			using HttpClient httpClient = _httpClientFactory.CreateClient(HttpClientName);

			using HttpResponseMessage response = await httpClient
				.GetAsync($"{ip}{_ipStackInitializer.IpStackSettings.ApiPostfix}", CancellationToken.None)
				.ConfigureAwait(false);

			response.EnsureSuccessStatusCode();

			return await response.Content.ReadAsStringAsync(CancellationToken.None).ConfigureAwait(false);
		}

		/// <summary>
		/// Detects IpStack's error envelope, which arrives with an HTTP 200 status. Without this, an invalid
		/// access key and an exhausted quota are indistinguishable from "this IP is unknown".
		/// </summary>
		private static bool TryReadError(string responseBody, out IpStackError? error)
		{
			error = null;

			IpStackErrorResponse? errorResponse = JsonSerializer.Deserialize<IpStackErrorResponse>(responseBody);
			if (errorResponse?.Success == false)
			{
				error = errorResponse.Error;
				return true;
			}

			return false;
		}
	}
}
