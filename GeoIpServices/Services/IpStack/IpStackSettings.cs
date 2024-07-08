namespace GeoIpServices.Services.IpStack
{
	public sealed class IpStackSettings
	{
		public required Uri ApiPrefix { get; init; }
		public required string ApiPostfix { get; init; }
	}
}
