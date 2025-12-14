using GeoIpServices.Common;
using Microsoft.Extensions.Configuration;

namespace GeoIpServices.Services.IpStack
{
	public sealed class IpStackInitializer
	{
		internal readonly IpStackSettings IpStackSettings;

		public IpStackInitializer(IConfiguration configuration)
		{
			var ipStackConfig = configuration.GetSection($"GeoIpSettings:{GeoIpInfoProvider.IpStack}");

			var apiPrefix = ipStackConfig["ApiPrefix"];
			var apiPostfix = ipStackConfig["ApiPostfix"];

			if (string.IsNullOrWhiteSpace(apiPrefix))
			{
				throw new InvalidOperationException("GeoIpSettings:IpStack:ApiPrefix is required but was not configured.");
			}

			if (string.IsNullOrWhiteSpace(apiPostfix))
			{
				throw new InvalidOperationException("GeoIpSettings:IpStack:ApiPostfix is required but was not configured.");
			}

			if (!apiPostfix.StartsWith("?access_key="))
			{
				throw new InvalidOperationException("GeoIpSettings:IpStack:ApiPostfix must start with '?access_key='.");
			}

			IpStackSettings = new IpStackSettings()
			{
				ApiPrefix = new Uri(apiPrefix),
				ApiPostfix = apiPostfix
			};
		}
	}
}
