using System.Text.RegularExpressions;

namespace Einvoicing.Application.Helpers;

public static class MatriculeFiscalHelper
{
    private static readonly Regex NormalizedPattern = new(@"^[0-9]{7}[A-Z]([A-Z]{2}[0-9]{3})?$", RegexOptions.Compiled);

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var upper = value.Trim().ToUpperInvariant();
        return Regex.Replace(upper, @"[^A-Z0-9]", string.Empty);
    }

    public static bool IsValid(string? value)
    {
        var normalized = Normalize(value);
        return NormalizedPattern.IsMatch(normalized);
    }
}
