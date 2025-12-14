using GeoIpServices.Common;
using GeoIpServices.Common.DTOs;
using GeoIpServices.Database;
using GeoIpServices.Database.DTOs;
using GeoIpServices.Services.IpStack;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
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

			GeoIpInfoSession? session = null;
			try
			{
				session = await _geoIpDbService.GetOrCreateAndGetLatestSession(ipV4);
				if (session is null)
				{
					_logger.LogWarning("Failed to create or retrieve session for IP: {IpAddress}", ipV4);
					return null;
				}

				Queue<GeoIpInfoProvider> geoIpInfoProvidersQueue;
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
						_ => throw new NotSupportedException($"GeoIpInfoProvider '{geoIpInfoProvidersQueue.Peek()}' is not supported."),
					};

					if (geoIpInfoFromIpv4Response is not null)
					{
						break;
					}

					geoIpInfoProvidersQueue.Dequeue();
				}

				if (session.GeoIpInfoProvidersQueue != geoIpInfoProvidersQueue)
				{
					session.GeoIpInfoProvidersQueue = geoIpInfoProvidersQueue;
					await _geoIpDbService.UpdateSession(session);
				}

				if (geoIpInfoFromIpv4Response is null)
				{
					_logger.LogWarning("Unable to fetch geo info for IP: {IpAddress}, SessionId: {SessionId}", ipV4, session.SessionId);
				}
				else
				{
					session.IsCompleted = true;
					await _geoIpDbService.UpdateSession(session);
				}

				return geoIpInfoFromIpv4Response;
			}
			catch (MongoException ex)
			{
				_logger.LogError(ex, "Database error while processing IP: {IpAddress}, SessionId: {SessionId}", ipV4, session?.SessionId);
				return null;
			}
			catch (NotSupportedException ex)
			{
				_logger.LogError(ex, "Unsupported provider encountered for IP: {IpAddress}, SessionId: {SessionId}", ipV4, session?.SessionId);
				return null;
			}
		}
	}
}
