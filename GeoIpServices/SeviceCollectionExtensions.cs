using GeoIpCommon;
using Microsoft.Extensions.DependencyInjection;

namespace GeoIpServices
{
	public static class SeviceCollectionExtensions
	{
		public static void AddSMSwitchServices(this IServiceCollection services)
		{
			services.AddSingleton<GeoIpInitializer>();
		}
	}
}
