using GeoIpCommon;
using MongoDB.Bson.Serialization.Attributes;

namespace GeoIpServices.Database.DTOs
{
	public sealed class GeoIpInfoSession
	{
		[BsonId]
		public required string SessionId { get; init; }
		public required string IpV4 { get; init; }
		public Queue<GeoIpInfoProvider>? GeoIpInfoProvidersQueue { get; set; }
		public required DateTimeOffset StartTimeUTC { get; init; } = DateTimeOffset.UtcNow;
		public DateTimeOffset? SuccessfullyCompletedTimestampUTC { get; set; }
		public DateTimeOffset ExpiryTimeUTC { get; init; }

		internal bool HasNotExpired() => SuccessfullyCompletedTimestampUTC == null && ExpiryTimeUTC < DateTimeOffset.UtcNow;
	}
}
