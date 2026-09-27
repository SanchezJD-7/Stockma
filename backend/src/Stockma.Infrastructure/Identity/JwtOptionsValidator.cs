using System.Text;
using Microsoft.Extensions.Options;

namespace Stockma.Infrastructure.Identity;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience es obligatorio.");
        }

        if (Encoding.UTF8.GetByteCount(options.Key ?? string.Empty) < JwtOptions.MinimumKeyBytes)
        {
            failures.Add($"Jwt:Key debe tener al menos {JwtOptions.MinimumKeyBytes} bytes para HMAC-SHA256.");
        }

        if (options.ExpiresMinutes is <= 0 or > JwtOptions.MaximumExpiresMinutes)
        {
            failures.Add($"Jwt:ExpiresMinutes debe estar entre 1 y {JwtOptions.MaximumExpiresMinutes} (NFR-004).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
