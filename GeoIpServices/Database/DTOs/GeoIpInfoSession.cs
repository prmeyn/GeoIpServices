using GeoIpServices.Common;
using MongoDB.Bson.Serialization.Attributes;

namespace GeoIpServices.Database.DTOs
{
	public sealed class GeoIpInfoSession
	{
		[BsonId]
		public required string SessionId { get; init; }
		public required string IpV4 { get; init; }
		public Queue<GeoIpInfoProvider>? GeoIpInfoProvidersQueue { get; set; }
		public required DateTime CreatedAtUTC { get; init; } = DateTime.UtcNow;
		public bool IsCompleted { get; set; }
	}
}
