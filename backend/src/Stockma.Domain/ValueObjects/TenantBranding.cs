using System.Text.RegularExpressions;

namespace Stockma.Domain.ValueObjects;
public sealed partial record TenantBranding
{
    public TenantBranding(string primary, string primaryActive, string primaryBg)
    {
        Primary = Normalize(primary, nameof(primary));
        PrimaryActive = Normalize(primaryActive, nameof(primaryActive));
        PrimaryBg = Normalize(primaryBg, nameof(primaryBg));
    }

    public string Primary { get; private init; }
    public string PrimaryActive { get; private init; }
    public string PrimaryBg { get; private init; }

    private static string Normalize(string color, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(color) || !HexColor().IsMatch(color))
        {
            throw new ArgumentException(
                $"El color debe ser hexadecimal con formato #rgb o #rrggbb. Recibido: '{color}'.",
                parameterName);
        }

        return color.ToLowerInvariant();
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColor();
}
