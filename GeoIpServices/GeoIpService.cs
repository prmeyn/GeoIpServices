using GeoIpServices.Common;
using GeoIpServices.Common.DTOs;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;

namespace GeoIpServices
{
	/// <summary>
	/// The default <see cref="IGeoInfoService"/>: walks the configured providers in priority order and
	/// returns the first answer.
	/// </summary>
	public sealed class GeoIpService : IGeoInfoService
	{
		private readonly GeoIpControls _geoIpControls;
		private readonly IReadOnlyDictionary<GeoIpInfoProvider, IGeoIpProvider> _providers;
		private readonly ILogger<GeoIpService> _logger;

		/// <summary>Creates the service over every registered <see cref="IGeoIpProvider"/>.</summary>
		/// <exception cref="InvalidOperationException">
		/// A provider named in <c>Priority</c> has no registered implementation.
		/// </exception>
		public GeoIpService(
			GeoIpInitializer geoIpInitializer,
			IEnumerable<IGeoIpProvider> providers,
			ILogger<GeoIpService> logger)
		{
			ArgumentNullException.ThrowIfNull(geoIpInitializer);
			ArgumentNullException.ThrowIfNull(providers);

			_geoIpControls = geoIpInitializer.GeoIpControls;
			_logger = logger;
			_providers = providers.ToDictionary(provider => provider.Provider);

			foreach (GeoIpInfoProvider configuredProvider in _geoIpControls.Priority)
			{
				if (!_providers.ContainsKey(configuredProvider))
				{
					throw new InvalidOperationException(
						$"GeoIpSettings:Controls:Priority names provider '{configuredProvider}', but no implementation of {nameof(IGeoIpProvider)} is registered for it.");
				}
			}
		}

		/// <inheritdoc />
		public async Task<GeoIpInfo?> GetGeoIpInfoFromIpv4(IPAddress? ipV4, CancellationToken cancellationToken = default)
		{
			IPAddress? ipAddress = NormalizeToIpv4(ipV4);
			if (ipAddress is null)
			{
				return null;
			}

			for (byte attempt = 0; attempt < _geoIpControls.MaxRoundRobinAttempts; attempt++)
			{
				foreach (GeoIpInfoProvider configuredProvider in _geoIpControls.Priority)
				{
					cancellationToken.ThrowIfCancellationRequested();

					GeoIpInfo? geoIpInfo = await _providers[configuredProvider]
						.GetGeoIpInfoAsync(ipAddress, cancellationToken)
						.ConfigureAwait(false);

					if (geoIpInfo is not null)
					{
						return geoIpInfo;
					}
				}
			}

			_logger.LogWarning(
				"Unable to fetch geo info for IP {IpAddress} after {MaxRoundRobinAttempts} attempt(s) across {ProviderCount} provider(s).",
				ipAddress,
				_geoIpControls.MaxRoundRobinAttempts,
				_geoIpControls.Priority.Count);

			return null;
		}

		private IPAddress? NormalizeToIpv4(IPAddress? address)
		{
			if (address is null)
			{
				return null;
			}

			if (address.AddressFamily == AddressFamily.InterNetwork)
			{
				return address;
			}

			// MapToIPv4() does no validation - for a native IPv6 address it simply reinterprets the last four
			// bytes, so 2001:db8::1 would silently become 0.0.0.1. That bogus address would then be looked up
			// upstream, billed against the API quota, and cached. Only unwrap addresses that really are IPv4.
			if (address.IsIPv4MappedToIPv6)
			{
				return address.MapToIPv4();
			}

			_logger.LogWarning("Ignoring {IpAddress}: only IPv4 and IPv4-mapped IPv6 addresses are supported.", address);
			return null;
		}
	}
}
