using GeoIpServices.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GeoIpServices.Services.IpStack
{
	public sealed class IpStackInitializer
	{
		internal readonly IpStackSettings IpStackSettings;

		public IpStackInitializer(
					IConfiguration configuration,
					ILogger<IpStackInitializer> logger)
		{
			try
			{

				var ipStackConfig = configuration.GetSection($"GeoIpSettings:{GeoIpInfoProvider.IpStack}");

				IpStackSettings = new IpStackSettings()
				{
					ApiPrefix = new Uri(ipStackConfig["ApiPrefix"]),
					ApiPostfix = ipStackConfig["ApiPostfix"]
				};
				if (!IpStackSettings?.ApiPostfix?.StartsWith("?access_key=") ?? true)
				{
					logger.LogCritical("IpStack ApiPostfix needs to start with \'?access_key=\'");
				}
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Unable to initialize IpStack");
			}
		}
	}
}
