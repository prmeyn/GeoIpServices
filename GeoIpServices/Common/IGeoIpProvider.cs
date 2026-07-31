using GeoIpServices.Common.DTOs;
using System.Net;

namespace GeoIpServices.Common
{
	/// <summary>
	/// A single upstream geolocation provider.
	/// </summary>
	/// <remarks>
	/// This is the extension point for adding providers. Consumers should resolve
	/// <see cref="IGeoInfoService"/> instead, which applies the configured priority order and retry budget
	/// across every registered implementation of this interface.
	/// </remarks>
	public interface IGeoIpProvider
	{
		/// <summary>Identifies which <see cref="GeoIpInfoProvider"/> this implementation serves.</summary>
		GeoIpInfoProvider Provider { get; }

		/// <summary>
		/// Looks up <paramref name="ipAddress"/>, returning <see langword="null"/> when the provider has no
		/// answer or the lookup failed. Implementations are expected to handle their own transport and
		/// deserialization errors rather than throwing.
		/// </summary>
		Task<GeoIpInfo?> GetGeoIpInfoAsync(IPAddress ipAddress, CancellationToken cancellationToken);
	}
}
