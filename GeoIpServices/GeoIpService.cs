using GeoIpCommon;
using GeoIpCommon.DTOs;
using GeoIpStack;
using Microsoft.Extensions.Logging;
using System.Net;

namespace GeoIpServices
{
	public class GeoIpService : IGeoInfoService
	{
		private readonly GeoIpInitializer _geoIpInitializer;
		private readonly IpStackService _ipStackService;
		private readonly ILogger<GeoIpService> _logger;

		public GeoIpService(
			GeoIpInitializer geoIpInitializer,
			IpStackService ipStackService,
			ILogger<GeoIpService> logger)
		{
			_geoIpInitializer = geoIpInitializer;
			_ipStackService = ipStackService;
			_logger = logger;
		}

		public async Task<GeoIpInfo?> GetGeoIpInfoFromIpv4(IPAddress? ipV4)
		{
			GeoIpInfo geoIpInfoResponse = null;
			// todo
			return await _ipStackService.GetGeoIpInfoFromIpv4(ipV4);
			// todo

		}
	}
}
