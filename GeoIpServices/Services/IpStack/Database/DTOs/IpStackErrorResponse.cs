using System.Text.Json.Serialization;

namespace GeoIpServices.Services.IpStack.Database.DTOs
{
	/// <summary>
	/// IpStack reports failures such as an invalid access key or an exhausted quota as HTTP 200 with this
	/// envelope in the body, so they have to be detected by shape rather than by status code.
	/// </summary>
	internal sealed class IpStackErrorResponse
	{
		[JsonPropertyName("success")] public bool? Success { get; set; }
		[JsonPropertyName("error")] public IpStackError? Error { get; set; }
	}

	internal sealed class IpStackError
	{
		[JsonPropertyName("code")] public int? Code { get; set; }
		[JsonPropertyName("type")] public string? Type { get; set; }
		[JsonPropertyName("info")] public string? Info { get; set; }
	}
}
