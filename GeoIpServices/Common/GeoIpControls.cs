namespace GeoIpServices.Common
{
	public sealed class GeoIpControls
	{
		public int SessionTimeoutInSeconds { get; init; }
		public int CacheDurationInHours { get; init; }
		public required HashSet<GeoIpInfoProvider> Priority { get; init; }
		public byte MaxRoundRobinAttempts { get; init; }
	}
}
