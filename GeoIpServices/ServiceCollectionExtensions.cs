using GeoIpServices.Common;
using GeoIpServices.Services.IpStack;
using GeoIpServices.Services.IpStack.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GeoIpServices
{
	/// <summary>
	/// Registers GeoIpServices with the dependency injection container.
	/// </summary>
	public static class ServiceCollectionExtensions
	{
		/// <summary>
		/// Registers the geolocation services. Requires <c>AddMongoDbServices()</c> to have been called, and
		/// the <c>GeoIpSettings</c> configuration section to be present.
		/// </summary>
		/// <remarks>
		/// A hosted service is registered alongside the services to validate configuration and create the
		/// MongoDB indexes during startup, so problems fail the host rather than the first request.
		/// </remarks>
		/// <returns>The same <see cref="IServiceCollection"/>, for chaining.</returns>
		public static IServiceCollection AddGeoIpServices(this IServiceCollection services)
		{
			ArgumentNullException.ThrowIfNull(services);

			services.TryAddSingleton<GeoIpInitializer>();
			services.TryAddSingleton<IpStackInitializer>();
			services.TryAddSingleton<IpStackDbService>();

			// A named client resolved per request, rather than one HttpClient captured for the lifetime of a
			// singleton: holding on to a factory client defeats handler rotation, which is what keeps the
			// connection pool from pinning to a stale DNS result.
			services.AddHttpClient(IpStackService.HttpClientName, (serviceProvider, httpClient) =>
			{
				IpStackSettings ipStackSettings = serviceProvider.GetRequiredService<IpStackInitializer>().IpStackSettings;
				httpClient.BaseAddress = ipStackSettings.ApiPrefix;
				httpClient.Timeout = ipStackSettings.RequestTimeout;
			});

			services.TryAddSingleton<IpStackService>();
			services.TryAddEnumerable(ServiceDescriptor.Singleton<IGeoIpProvider, IpStackService>(
				static serviceProvider => serviceProvider.GetRequiredService<IpStackService>()));

			services.TryAddSingleton<GeoIpService>();
			services.TryAddSingleton<IGeoInfoService>(static serviceProvider => serviceProvider.GetRequiredService<GeoIpService>());

			services.AddHostedService<GeoIpStartupService>();

			return services;
		}
	}
}
