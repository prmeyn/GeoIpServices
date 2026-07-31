using MongoDB.Bson;
using MongoDB.Driver;

namespace GeoIpServices.Tests.Integration;

internal static class MongoTestEnvironment
{
	internal static string ConnectionString =>
		Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? "mongodb://localhost:27017";

	private static readonly Lazy<bool> Availability = new(Probe, LazyThreadSafetyMode.ExecutionAndPublication);

	internal static bool IsAvailable => Availability.Value;

	private static bool Probe()
	{
		try
		{
			MongoClientSettings settings = MongoClientSettings.FromConnectionString(ConnectionString);
			settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
			settings.ConnectTimeout = TimeSpan.FromSeconds(3);

			new MongoClient(settings)
				.GetDatabase("admin")
				.RunCommand<BsonDocument>(new BsonDocument("ping", 1));

			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}
}

/// <summary>
/// A <see cref="FactAttribute"/> that skips instead of failing when no MongoDB server is reachable, so the
/// suite still runs on a machine without one.
/// </summary>
public sealed class MongoFactAttribute : FactAttribute
{
	public MongoFactAttribute()
	{
		if (!MongoTestEnvironment.IsAvailable)
		{
			Skip = $"MongoDB is not reachable at {MongoTestEnvironment.ConnectionString}.";
		}
	}
}
