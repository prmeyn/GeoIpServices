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
		[Obsolete("Use GetGeoIpInfoAsync, which also resolves native IPv6.")]
		Task<GeoIpInfo?> GetGeoIpInfoFromIpv4(IPAddress? ipV4, CancellationToken cancellationToken = default);

		/// <summary>
		/// Resolves <paramref name="ipAddress"/> to geolocation information.
		/// </summary>
		/// <param name="ipAddress">
		/// An IPv4 address, an IPv4-mapped IPv6 address (which is unwrapped), or a native IPv6 address, which is
		/// looked up - and cached - by its /64 network. Addresses that cannot be geolocated (loopback,
		/// link-local, unique-local, multicast) return <see langword="null"/>; see the README for details.
		/// </param>
		/// <param name="cancellationToken">Cancels the lookup.</param>
		/// <returns>
		/// The geolocation information, or <see langword="null"/> if the address is unusable or no configured
		/// provider could answer.
		/// </returns>
		/// <remarks>
		/// The default implementation falls back to <see cref="GetGeoIpInfoFromIpv4"/>, so an existing
		/// implementation of this interface keeps compiling; <see cref="GeoIpService"/> overrides it.
		/// </remarks>
#pragma warning disable CS0618 // the fallback is the obsolete IPv4-only path by design
		Task<GeoIpInfo?> GetGeoIpInfoAsync(IPAddress? ipAddress, CancellationToken cancellationToken = default) =>
			GetGeoIpInfoFromIpv4(ipAddress, cancellationToken);
#pragma warning restore CS0618
	}
}
