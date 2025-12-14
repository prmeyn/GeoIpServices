using Microsoft.Extensions.Configuration;

namespace GeoIpServices.Common
{
	public sealed class GeoIpInitializer
	{
		private const int DefaultSessionTimeoutInSeconds = 300;
		private const int DefaultCacheDurationInHours = 24;

		public readonly GeoIpControls GeoIpControls;

		public GeoIpInitializer(IConfiguration configuration)
		{
			var geoIpControlsConfig = configuration.GetSection("GeoIpSettings:Controls");
			GeoIpControls = new GeoIpControls() {
				MaxRoundRobinAttempts = byte.TryParse(geoIpControlsConfig["MaxRoundRobinAttempts"], out byte maxRoundRobinAttempts) ? maxRoundRobinAttempts : (byte)1,
				SessionTimeoutInSeconds = int.TryParse(geoIpControlsConfig["SessionTimeoutInSeconds"], out int sessionTimeout) ? sessionTimeout : DefaultSessionTimeoutInSeconds,
				CacheDurationInHours = int.TryParse(geoIpControlsConfig["CacheDurationInHours"], out int cacheDuration) ? cacheDuration : DefaultCacheDurationInHours,
				Priority = GetPriority(geoIpControlsConfig?.GetRequiredSection("Priority")?.Get<string[]>())
			};
		}

		private static HashSet<GeoIpInfoProvider> GetPriority(string[]? value)
		{
			if (value is null || value.Length < 1)
			{
				throw new InvalidOperationException("GeoIpSettings:Controls:Priority is required but was not configured.");
			}
			var valuesFromConfig = value.Where(p => Enum.TryParse(p, out GeoIpInfoProvider _)).Select(p => Enum.Parse<GeoIpInfoProvider>(p)).ToHashSet();
			if (valuesFromConfig.Count < 1)
			{
				throw new InvalidOperationException("GeoIpSettings:Controls:Priority must contain at least one valid provider.");
			}
			return valuesFromConfig;
		}
	}
}
