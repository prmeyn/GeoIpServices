using GeoIpServices.Common.DTOs;
using System.Net;

namespace GeoIpServices.Common
{
	/// <summary>
	/// Resolves an IP address to geolocation information, caching results to limit upstream API usage.
	/// </summary>
	public interface IGeoInfoService
	{
		/// <summary>
		/// Resolves <paramref name="ipV4"/> to geolocation information.
		/// </summary>
		/// <param name="ipV4">
		/// An IPv4 address, or an IPv4-mapped IPv6 address (which is unwrapped). Native IPv6 addresses are
		/// not supported and return <see langword="null"/>; see the README for details.
		/// </param>
		/// <param name="cancellationToken">Cancels the lookup.</param>
		/// <returns>
		/// The geolocation information, or <see langword="null"/> if the address is unusable or no configured
		/// provider could answer.
		/// </returns>
		Task<GeoIpInfo?> GetGeoIpInfoFromIpv4(IPAddress? ipV4, CancellationToken cancellationToken = default);
	}
}
