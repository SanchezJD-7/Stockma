using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Stockma.Application.Identity;

namespace Stockma.Infrastructure.Identity;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions options;
    private readonly TimeProvider timeProvider;
    private readonly SigningCredentials credentials;

    public JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        this.options = options.Value;
        this.timeProvider = timeProvider;

        if (string.IsNullOrWhiteSpace(this.options.Issuer))
        {
            throw new ArgumentException("El issuer del JWT es obligatorio.", nameof(JwtOptions.Issuer));
        }

        if (string.IsNullOrWhiteSpace(this.options.Audience))
        {
            throw new ArgumentException("El audience del JWT es obligatorio.", nameof(JwtOptions.Audience));
        }

        var keyBytes = Encoding.UTF8.GetBytes(this.options.Key ?? string.Empty);

        if (keyBytes.Length < JwtOptions.MinimumKeyBytes)
        {
            throw new ArgumentException(
                $"La clave de firma debe tener al menos {JwtOptions.MinimumKeyBytes} bytes para HMAC-SHA256.",
                nameof(JwtOptions.Key));
        }

        if (this.options.ExpiresMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(JwtOptions.ExpiresMinutes),
                this.options.ExpiresMinutes,
                "La vigencia del access token debe ser positiva.");
        }

        if (this.options.ExpiresMinutes > JwtOptions.MaximumExpiresMinutes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(JwtOptions.ExpiresMinutes),
                this.options.ExpiresMinutes,
                $"NFR-004: el access token no puede exceder {JwtOptions.MaximumExpiresMinutes} minutos.");
        }

        credentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken Create(Guid userId, Guid tenantId, IReadOnlyCollection<string> roles)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El UserId no puede ser Guid.Empty.", nameof(userId));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "El TenantId no puede ser Guid.Empty: un token sin tid dejaría al portador fuera de todo aislamiento.",
                nameof(tenantId));
        }

        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(options.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString()),
            new(StockmaClaimTypes.TenantId, tenantId.ToString()),
        };

        claims.AddRange(roles.Select(role => new Claim(StockmaClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            (int)(expiresAt - issuedAt).TotalSeconds);
    }
}
