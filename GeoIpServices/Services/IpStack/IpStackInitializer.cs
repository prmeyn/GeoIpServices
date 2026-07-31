using GeoIpServices.Common;
using Microsoft.Extensions.Configuration;

namespace GeoIpServices.Services.IpStack
{
	/// <summary>
	/// Reads and validates the <c>GeoIpSettings:IpStack</c> configuration section.
	/// </summary>
	internal sealed class IpStackInitializer
	{
		private const string AccessKeyPrefix = "?access_key=";
		private const int DefaultTimeoutInSeconds = 10;

		internal IpStackSettings IpStackSettings { get; }

		public IpStackInitializer(IConfiguration configuration)
		{
			ArgumentNullException.ThrowIfNull(configuration);

			string sectionName = $"GeoIpSettings:{GeoIpInfoProvider.IpStack}";
			IConfigurationSection ipStackConfig = configuration.GetSection(sectionName);

			IpStackSettings = new IpStackSettings()
			{
				ApiPrefix = ReadApiPrefix(sectionName, ipStackConfig["ApiPrefix"]),
				ApiPostfix = ReadApiPostfix(sectionName, ipStackConfig["ApiPostfix"]),
				RequestTimeout = ReadRequestTimeout(sectionName, ipStackConfig["TimeoutInSeconds"])
			};
		}

		private static Uri ReadApiPrefix(string sectionName, string? configuredValue)
		{
			if (string.IsNullOrWhiteSpace(configuredValue))
			{
				throw new InvalidOperationException($"{sectionName}:ApiPrefix is required but was not configured.");
			}

			if (!Uri.TryCreate(configuredValue, UriKind.Absolute, out Uri? apiPrefix))
			{
				throw new InvalidOperationException($"{sectionName}:ApiPrefix ('{configuredValue}') is not a valid absolute URI.");
			}

			if (!string.IsNullOrEmpty(apiPrefix.Query) || !string.IsNullOrEmpty(apiPrefix.Fragment))
			{
				throw new InvalidOperationException(
					$"{sectionName}:ApiPrefix must not contain a query string or fragment. The access key belongs in ApiPostfix.");
			}

			// Resolving a relative URI against a base address whose path has no trailing slash replaces the
			// last segment, so "https://api.ipstack.com/v1" would quietly request "https://api.ipstack.com/1.2.3.4".
			if (!apiPrefix.AbsolutePath.EndsWith('/'))
			{
				apiPrefix = new Uri(apiPrefix.AbsoluteUri + "/");
			}

			return apiPrefix;
		}

		private static string ReadApiPostfix(string sectionName, string? configuredValue)
		{
			// Deliberately never include the configured value in these messages - it carries the access key.
			if (string.IsNullOrWhiteSpace(configuredValue))
			{
				throw new InvalidOperationException($"{sectionName}:ApiPostfix is required but was not configured.");
			}

			if (!configuredValue.StartsWith(AccessKeyPrefix, StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"{sectionName}:ApiPostfix must start with '{AccessKeyPrefix}'.");
			}

			if (configuredValue.Length == AccessKeyPrefix.Length)
			{
				throw new InvalidOperationException(
					$"{sectionName}:ApiPostfix contains no access key after '{AccessKeyPrefix}'. Supply your IpStack access key.");
			}

			return configuredValue;
		}

		private static TimeSpan ReadRequestTimeout(string sectionName, string? configuredValue)
		{
			if (string.IsNullOrWhiteSpace(configuredValue))
			{
				return TimeSpan.FromSeconds(DefaultTimeoutInSeconds);
			}

			if (!int.TryParse(configuredValue, out int timeoutInSeconds) || timeoutInSeconds < 1)
			{
				throw new InvalidOperationException(
					$"{sectionName}:TimeoutInSeconds must be a positive whole number, but was '{configuredValue}'.");
			}

			return TimeSpan.FromSeconds(timeoutInSeconds);
		}
	}
}
