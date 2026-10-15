using System.Text.RegularExpressions;
using Stockma.Domain.Exceptions;

namespace Stockma.Domain.ValueObjects;

public sealed record CountryPhoneRule(string CountryCode, string NumberPattern, string Description);

public static class CountryPhoneRules
{
    private static readonly Dictionary<string, CountryPhoneRule> Rules = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CO"] = new("CO", @"^\+573\d{9}$", "Colombia: +57 y móvil de 10 dígitos que empieza con 3"),
    };

    public static CountryPhoneRule? Find(string? countryCode) =>
        countryCode is not null && Rules.TryGetValue(countryCode.Trim(), out var rule) ? rule : null;

    public static void Validate(string? countryCode, string e164PhoneNumber)
    {
        var rule = Find(countryCode);

        if (rule is null) return;

        if (!Regex.IsMatch(e164PhoneNumber, rule.NumberPattern))
        {
            throw new ValidationFailedException(
                $"El número no corresponde a un móvil de {rule.Description}.");
        }
    }
}
