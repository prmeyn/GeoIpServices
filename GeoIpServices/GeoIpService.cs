using GeoIpServices.Common;
using GeoIpServices.Common.DTOs;
using GeoIpServices.Database;
using GeoIpServices.Database.DTOs;
using GeoIpServices.Services.IpStack;
using Microsoft.Extensions.Logging;
using System.Net;

namespace GeoIpServices
{
	public class GeoIpService : IGeoInfoService
	{
		private readonly GeoIpInitializer _geoIpInitializer;
		private readonly GeoIpDbService _geoIpDbService;
		private readonly IpStackService _ipStackService;
		private readonly ILogger<GeoIpService> _logger;

		public GeoIpService(
			GeoIpInitializer geoIpInitializer,
			GeoIpDbService geoIpDbService,
			IpStackService ipStackService,
			ILogger<GeoIpService> logger)
		{
			_geoIpInitializer = geoIpInitializer;
			_geoIpDbService = geoIpDbService;
			_ipStackService = ipStackService;
			_logger = logger;
		}

		public async Task<GeoIpInfo?> GetGeoIpInfoFromIpv4(IPAddress? ipV4)
		{
			ipV4 = ipV4?.MapToIPv4();
			if (ipV4 is null)
			{
				return null;
			}
			GeoIpInfo geoIpInfoResponse = null;
			GeoIpInfoSession session = null;
			try
			{
				session = await _geoIpDbService.GetOrCreateAndGetLatestSession(ipV4);

				Queue<GeoIpInfoProvider> geoIpInfoProvidersQueue = null;
				if (session.GeoIpInfoProvidersQueue?.Any() ?? false)
				{
					geoIpInfoProvidersQueue = session.GeoIpInfoProvidersQueue;
				}
				else
				{
					geoIpInfoProvidersQueue = new();
					HashSet<GeoIpInfoProvider> geoIpInfoProviders = _geoIpInitializer.GeoIpControls.Priority;
					for (int i = 0; i < _geoIpInitializer.GeoIpControls.MaxRoundRobinAttempts; i++)
					{
						foreach (GeoIpInfoProvider geoIpInfoProvider in geoIpInfoProviders)
						{
							geoIpInfoProvidersQueue.Enqueue(geoIpInfoProvider);
						}
					}
				}

				if (geoIpInfoProvidersQueue.Count == 0)
				{
					return null;
				}

				GeoIpInfo? geoIpInfoFromIpv4Response = null;
				while (geoIpInfoProvidersQueue.Count > 0)
				{

					geoIpInfoFromIpv4Response = geoIpInfoProvidersQueue.Peek() switch
					{
						GeoIpInfoProvider.IpStack => await _ipStackService.GetGeoIpInfoFromIpv4(ipV4),
						_ => throw new NotImplementedException(),
					};

					if (geoIpInfoFromIpv4Response is not null)
					{
						break;
					}
					else
					{
						geoIpInfoProvidersQueue.Dequeue();
					}
				}
				if (session.GeoIpInfoProvidersQueue != geoIpInfoProvidersQueue)
				{
					session.GeoIpInfoProvidersQueue = geoIpInfoProvidersQueue;
					await _geoIpDbService.UpdateSession(session);
				}



				if (geoIpInfoFromIpv4Response is null)
				{
					_logger.LogCritical($"Unable to fetch info for IP: {ipV4} with SessionId{session?.SessionId}");
				}
				else
				{
					session.SuccessfullyCompletedTimestampUTC = DateTime.UtcNow;
					await _geoIpDbService.UpdateSession(session);
				}
				return geoIpInfoFromIpv4Response;
			}
			catch (Exception exception)
			{
				_logger.LogCritical(exception, $"Unable to fetch info for IP: {ipV4} with SessionId{session?.SessionId}");
			}
			return null;
		}
	}
}
