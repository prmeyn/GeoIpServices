namespace GeoIpServices.Common
{
	/// <summary>
	/// Strict, name-only enum parsing.
	/// </summary>
	/// <remarks>
	/// <see cref="Enum.TryParse{TEnum}(string, bool, out TEnum)"/> also accepts numeric strings and does not
	/// check that the result is a defined member, so <c>"Priority": [ "0" ]</c> would silently resolve to the
	/// first provider and a country code of <c>"999"</c> would produce an undefined
	/// <c>CountryIsoCode</c>. This helper accepts member names only. It mirrors the equivalent guard in
	/// <c>HumanLanguages.HumanHelper</c>.
	/// </remarks>
	internal static class EnumParser
	{
		internal static bool TryParseName<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
		{
			result = default;

			if (string.IsNullOrWhiteSpace(value))
			{
				return false;
			}

			char first = value[0];
			if (char.IsAsciiDigit(first) || first == '-' || first == '+')
			{
				return false;
			}

			return Enum.TryParse(value, ignoreCase: true, out result) && Enum.IsDefined(result);
		}
	}
}
