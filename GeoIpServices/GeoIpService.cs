using GeoIpCommon;
using GeoIpCommon.DTOs;
using System.Net;

namespace GeoIpServices
{
	public class GeoIpService : IGeoInfoService
	{
		public Task<GeoIpInfo> GetGeoIpInfoFromIpv4(IPAddress ipV4)
		{
			throw new NotImplementedException();
		}
	}
}
