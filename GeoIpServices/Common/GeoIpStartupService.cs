using GeoIpServices.Services.IpStack;
using GeoIpServices.Services.IpStack.Database;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GeoIpServices.Common
{
	/// <summary>
	/// Moves configuration validation and index creation to startup, so both fail the host rather than the
	/// first live request.
	/// </summary>
	/// <remarks>
	/// The initializers are singletons and would otherwise be constructed lazily on the first lookup, turning
	/// a deploy-time misconfiguration into a runtime error. Injecting them here forces their validating
	/// constructors to run during <see cref="StartAsync"/>. Index creation is awaited for the same reason:
	/// discarded as an unobserved task, a failure would leave the collection unindexed and never expiring,
	/// with the application still reporting healthy.
	/// </remarks>
	internal sealed class GeoIpStartupService : IHostedService
	{
		private readonly GeoIpInitializer _geoIpInitializer;
		private readonly IpStackDbService _ipStackDbService;
		private readonly ILogger<GeoIpStartupService> _logger;

		public GeoIpStartupService(
			GeoIpInitializer geoIpInitializer,
			IpStackInitializer ipStackInitializer,
			IpStackDbService ipStackDbService,
			ILogger<GeoIpStartupService> logger)
		{
			ArgumentNullException.ThrowIfNull(ipStackInitializer);

			_geoIpInitializer = geoIpInitializer;
			_ipStackDbService = ipStackDbService;
			_logger = logger;
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			GeoIpControls controls = _geoIpInitializer.GeoIpControls;

			_logger.LogInformation(
				"GeoIpServices starting. Providers in priority order: {Priority}. Cache duration: {CacheDurationInHours}h. Max round-robin attempts: {MaxRoundRobinAttempts}.",
				string.Join(", ", controls.Priority),
				controls.CacheDurationInHours,
				controls.MaxRoundRobinAttempts);

			await _ipStackDbService.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
		}

		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
