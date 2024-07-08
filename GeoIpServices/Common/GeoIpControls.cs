namespace GeoIpServices.Common
{
	public sealed class GeoIpControls
	{
		public int SessionTimeoutInSeconds { get; init; }
		public HashSet<GeoIpInfoProvider> Priority { get; init; }
		public byte MaxRoundRobinAttempts { get; init; }
	}
}
