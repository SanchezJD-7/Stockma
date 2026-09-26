using System.Security.Cryptography;
using Stockma.Application.Identity;

namespace Stockma.Infrastructure.Identity;

public sealed class OtpGenerator : IOtpGenerator
{
    public const int Digits = 6;

    private const int UpperBound = 1_000_000;

    public string Generate() =>
        RandomNumberGenerator.GetInt32(UpperBound).ToString($"D{Digits}");
}
