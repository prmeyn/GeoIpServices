using GeoIpServices.Common.DTOs;
using System.Net;

namespace GeoIpServices.Common
{
	public interface IGeoInfoService
	{
		Task<GeoIpInfo?> GetGeoIpInfoFromIpv4(IPAddress ipV4);
	}
}
