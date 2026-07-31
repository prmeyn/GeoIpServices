using GeoIpServices.Services.IpStack.Database.DTOs;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace GeoIpServices.Tests;

public sealed class IpStackInfoSerializationTests
{
	/// <summary>
	/// The regression guard for the cache TTL. MongoDB's TTL monitor only deletes documents whose indexed
	/// field is a BSON date; the driver serializes <see cref="DateTimeOffset"/> as a sub-document, which the
	/// monitor silently ignores. If this assertion ever fails, cached entries stop expiring.
	/// </summary>
	[Fact]
	public void ResponseTimeStampUTC_SerializesAsABsonDate()
	{
		IpStackInfo ipStackInfo = new() { Id = "1.2.3.4", ResponseTimeStampUTC = DateTime.UtcNow };

		BsonDocument document = ipStackInfo.ToBsonDocument();

		Assert.Equal(BsonType.DateTime, document[nameof(IpStackInfo.ResponseTimeStampUTC)].BsonType);
	}

	[Fact]
	public void IpStackInfo_RoundTripsThroughBson()
	{
		IpStackInfo ipStackInfo = new()
		{
			Id = "1.2.3.4",
			CountryCode = "DK",
			City = "Copenhagen",
			ResponseTimeStampUTC = new DateTime(2026, 07, 30, 12, 00, 00, DateTimeKind.Utc)
		};

		IpStackInfo roundTripped = BsonSerializer.Deserialize<IpStackInfo>(ipStackInfo.ToBsonDocument());

		Assert.Equal(ipStackInfo.Id, roundTripped.Id);
		Assert.Equal(ipStackInfo.CountryCode, roundTripped.CountryCode);
		Assert.Equal(ipStackInfo.City, roundTripped.City);
		Assert.Equal(ipStackInfo.ResponseTimeStampUTC, roundTripped.ResponseTimeStampUTC);
	}
}
