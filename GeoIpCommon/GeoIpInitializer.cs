using Microsoft.Extensions.Configuration;

namespace GeoIpCommon
{
	public sealed class GeoIpInitializer
	{
		public readonly GeoIpControls GeoIpControls;
		public GeoIpInitializer(IConfiguration configuration)
		{
			var geoIpControlsConfig = configuration.GetSection("GeoIpSettings:Controls");
			GeoIpControls = new GeoIpControls() { 
				Priority = getPriority(geoIpControlsConfig?.GetRequiredSection("Priority")?.Get<string[]>())
			};
		}

		private HashSet<GeoIpInfoProvider> getPriority(string[]? value)
		{
			if (value == null || value.Length < 1)
			{
				throw new Exception("GeoIpSettings:Controls:Priority list missing!");
			}
			var valuesFromConfig = value.Where(p => Enum.TryParse(p, out GeoIpInfoProvider _)).Select(p => Enum.Parse<GeoIpInfoProvider>(p)).ToHashSet();
			if (valuesFromConfig.Count() < 1)
			{
				throw new Exception("Priority list missing!!");
			}
			return valuesFromConfig;
		}
	}
}
