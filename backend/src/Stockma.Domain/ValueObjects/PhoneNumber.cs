using System.Text.RegularExpressions;
using Stockma.Domain.Exceptions;

namespace Stockma.Domain.ValueObjects;

public static partial class PhoneNumber
{
    public static string Parse(string? phoneNumber)
    {
        var normalized = phoneNumber?.Trim() ?? string.Empty;

        if (!E164Regex().IsMatch(normalized))
        {
            throw new ValidationFailedException(
                "El número debe estar en formato E.164: '+' seguido de 7 a 15 dígitos, "
                + "sin espacios ni símbolos (ej. +5491123456789).");
        }
        return normalized;
    }

    public static string Mask(string phoneNumber) =>
        phoneNumber.Length <= 8
            ? new string('*', phoneNumber.Length)
            : $"{phoneNumber[..6]}{new string('*', phoneNumber.Length - 8)}{phoneNumber[^2..]}";

    [GeneratedRegex(@"^\+[1-9]\d{6,14}$")]
    private static partial Regex E164Regex();
}
