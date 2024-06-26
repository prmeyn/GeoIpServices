using GeoIpCommon.DTOs;
using System.Net;

namespace GeoIpCommon
{
	public interface IGeoInfoService
	{
		Task<GeoIpInfo> GetGeoIpInfoFromIpv4(IPAddress ipV4);
	}
}
