using Microsoft.Extensions.Configuration;

namespace GeoIpServices.Common
{
	/// <summary>
	/// Reads and validates the <c>GeoIpSettings:Controls</c> configuration section.
	/// </summary>
	/// <remarks>
	/// Invalid values throw rather than falling back to a default, so a misconfiguration fails the deploy
	/// instead of quietly changing behaviour. <c>AddGeoIpServices</c> registers a hosted service that
	/// resolves this type during startup so the failure surfaces there rather than on the first request.
	/// </remarks>
	public sealed class GeoIpInitializer
	{
		private const string SectionName = "GeoIpSettings:Controls";
		private const byte DefaultMaxRoundRobinAttempts = 1;
		private const int DefaultCacheDurationInHours = 24;

		/// <summary>The validated controls.</summary>
		public GeoIpControls GeoIpControls { get; }

		/// <summary>Reads and validates the configuration section.</summary>
		/// <exception cref="InvalidOperationException">The section is missing or contains invalid values.</exception>
		public GeoIpInitializer(IConfiguration configuration)
		{
			ArgumentNullException.ThrowIfNull(configuration);

			IConfigurationSection controlsConfig = configuration.GetSection(SectionName);

			GeoIpControls = new GeoIpControls()
			{
				MaxRoundRobinAttempts = ReadMaxRoundRobinAttempts(controlsConfig["MaxRoundRobinAttempts"]),
				CacheDurationInHours = ReadCacheDurationInHours(controlsConfig["CacheDurationInHours"]),
				Priority = ReadPriority(controlsConfig.GetSection("Priority").Get<string[]>())
			};
		}

		private static byte ReadMaxRoundRobinAttempts(string? configuredValue)
		{
			if (string.IsNullOrWhiteSpace(configuredValue))
			{
				return DefaultMaxRoundRobinAttempts;
			}

			// Zero would drain the retry budget before any provider was called, so a lookup would return
			// null without a single upstream request and without anything to explain why.
			if (!byte.TryParse(configuredValue, out byte maxRoundRobinAttempts) || maxRoundRobinAttempts < 1)
			{
				throw new InvalidOperationException(
					$"{SectionName}:MaxRoundRobinAttempts must be a whole number between 1 and {byte.MaxValue}, but was '{configuredValue}'.");
			}

			return maxRoundRobinAttempts;
		}

		private static int ReadCacheDurationInHours(string? configuredValue)
		{
			if (string.IsNullOrWhiteSpace(configuredValue))
			{
				return DefaultCacheDurationInHours;
			}

			// A non-positive duration becomes a TTL index that expires documents the moment they are written
			// (or a negative expireAfterSeconds that MongoDB rejects outright).
			if (!int.TryParse(configuredValue, out int cacheDurationInHours) || cacheDurationInHours < 1)
			{
				throw new InvalidOperationException(
					$"{SectionName}:CacheDurationInHours must be a positive whole number, but was '{configuredValue}'.");
			}

			return cacheDurationInHours;
		}

		private static IReadOnlyList<GeoIpInfoProvider> ReadPriority(string[]? configuredNames)
		{
			if (configuredNames is null || configuredNames.Length < 1)
			{
				throw new InvalidOperationException($"{SectionName}:Priority is required but was not configured.");
			}

			List<GeoIpInfoProvider> priority = new(configuredNames.Length);
			foreach (string? configuredName in configuredNames)
			{
				// Unrecognised names are rejected rather than skipped: silently dropping a typo leaves the
				// caller with a confusing "no valid providers" error that never names the offending value.
				if (!EnumParser.TryParseName(configuredName, out GeoIpInfoProvider provider))
				{
					throw new InvalidOperationException(
						$"{SectionName}:Priority contains '{configuredName}', which is not a known provider. Valid providers are: {string.Join(", ", Enum.GetNames<GeoIpInfoProvider>())}.");
				}

				if (!priority.Contains(provider))
				{
					priority.Add(provider);
				}
			}

			return priority;
		}
	}
}
