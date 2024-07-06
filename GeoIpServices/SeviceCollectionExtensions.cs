using GeoIpCommon;
using GeoIpServices.Database;
using GeoIpStack;
using GeoIpStack.Database;
using Microsoft.Extensions.DependencyInjection;

namespace GeoIpServices
{
	public static class SeviceCollectionExtensions
	{
		public static void AddGeoIpServices(this IServiceCollection services)
		{
			services.AddSingleton<GeoIpInitializer>();
			services.AddSingleton<GeoIpDbService>();

			services.AddSingleton<IpStackInitializer>();
			services.AddSingleton<IpStackDbService>();
			services.AddHttpClient();
			services.AddTransient<IpStackService>();

			services.AddScoped<GeoIpService>();
		}
	}
}
