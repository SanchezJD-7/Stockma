using Stockma.Domain.Exceptions;

namespace Stockma.Domain.ValueObjects;

public static class DeviceIdentifier
{
    public const int MinLength = 16;
    public const int MaxLength = 128;

    public static string Normalize(string deviceId) => deviceId.Trim();

    public static string Parse(string? deviceId)
    {
        var normalized = deviceId is null ? string.Empty : Normalize(deviceId);

        if (normalized.Length < MinLength || normalized.Length > MaxLength)
        {
            throw new ValidationFailedException(
                $"El identificador del dispositivo es obligatorio y debe tener entre {MinLength} y {MaxLength} caracteres.");
        }

        return normalized;
    }
}
