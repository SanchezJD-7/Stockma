using Stockma.Domain.Common;
using Stockma.Domain.Waitlist;

namespace Stockma.Domain.Entities;

public class WaitlistEntry : IAggregateRoot
{
    private WaitlistEntry()
    {
        FullName = string.Empty;
        CountryCode = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
    }

    public WaitlistEntry(string fullName, string countryCode, string phone, string email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("El nombre completo es obligatorio", nameof(fullName));
        }

        fullName = fullName.Trim();
        if (fullName.Length < WaitlistRules.MinNameLength)
        {
            throw new ArgumentException($"El nombre completo debe tener al menos {WaitlistRules.MinNameLength} caracteres", nameof(fullName));
        }

        if (fullName.Length > WaitlistRules.MaxNameLength)
        {
            throw new ArgumentException($"El nombre completo no puede superar {WaitlistRules.MaxNameLength} caracteres", nameof(fullName));
        }

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            throw new ArgumentException("El prefijo del país es obligatorio", nameof(countryCode));
        }

        countryCode = countryCode.Trim();
        if (countryCode.Length > WaitlistRules.MaxCountryCodeLength)
        {
            throw new ArgumentException($"El prefijo del país no puede superar {WaitlistRules.MaxCountryCodeLength} caracteres", nameof(countryCode));
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new ArgumentException("El teléfono es obligatorio", nameof(phone));
        }

        phone = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (phone.Length is < WaitlistRules.MinPhoneDigits or > WaitlistRules.MaxPhoneDigits)
        {
            throw new ArgumentException($"El teléfono debe tener entre {WaitlistRules.MinPhoneDigits} y {WaitlistRules.MaxPhoneDigits} dígitos", nameof(phone));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("El correo es obligatorio", nameof(email));
        }

        email = email.Trim();
        if (email.Length > WaitlistRules.MaxEmailLength)
        {
            throw new ArgumentException($"El correo no puede superar {WaitlistRules.MaxEmailLength} caracteres", nameof(email));
        }

        Id = Guid.CreateVersion7();
        FullName = fullName;
        CountryCode = countryCode;
        Phone = phone;
        Email = email.ToLowerInvariant();
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string FullName { get; private set; }
    public string CountryCode { get; private set; }
    public string Phone { get; private set; }
    public string Email { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
