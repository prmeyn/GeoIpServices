namespace GeoIpServices.Services.IpStack
{
	internal sealed class IpStackSettings
	{
		/// <summary>The API base address. Always ends in a slash so relative URIs resolve against it correctly.</summary>
		internal required Uri ApiPrefix { get; init; }

		/// <summary>The query string carrying the access key. Treat as a secret - never log it.</summary>
		internal required string ApiPostfix { get; init; }

		/// <summary>Per-request timeout applied to the HTTP client.</summary>
		internal required TimeSpan RequestTimeout { get; init; }
	}
}
