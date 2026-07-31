namespace GeoIpServices.Common
{
	/// <summary>
	/// The validated contents of the <c>GeoIpSettings:Controls</c> configuration section.
	/// </summary>
	public sealed class GeoIpControls
	{
		/// <summary>
		/// How long a cached provider response is retained before MongoDB's TTL index removes it.
		/// </summary>
		public required int CacheDurationInHours { get; init; }

		/// <summary>
		/// The providers to query, in order of preference. Order is significant, which is why this is an
		/// ordered list rather than a set.
		/// </summary>
		public required IReadOnlyList<GeoIpInfoProvider> Priority { get; init; }

		/// <summary>
		/// How many times to cycle through <see cref="Priority"/> before giving up on a lookup.
		/// </summary>
		public required byte MaxRoundRobinAttempts { get; init; }
	}
}
