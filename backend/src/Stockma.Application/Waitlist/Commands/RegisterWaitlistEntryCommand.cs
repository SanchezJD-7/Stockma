using System.ComponentModel.DataAnnotations;
using MediatR;
using Microsoft.Extensions.Logging;
using Stockma.Application.Waitlist.Exceptions;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;
using Stockma.Domain.Waitlist;
using System.Net.Mail;

namespace Stockma.Application.Waitlist.Commands;

public sealed record RegisterWaitlistEntryCommand(
    [param: Required(ErrorMessage = "El nombre completo es obligatorio")]
    [param: StringLength(WaitlistRules.MaxNameLength, MinimumLength = WaitlistRules.MinNameLength, ErrorMessage = "El nombre completo debe tener entre 2 y 120 caracteres")]
    string FullName,
    [param: Required(ErrorMessage = "El prefijo del país es obligatorio")]
    [param: RegularExpression(@"^\+[0-9]{1,4}$", ErrorMessage = "El prefijo del país no es válido")]
    string CountryCode,
    [param: Required(ErrorMessage = "El teléfono es obligatorio")]
    [param: RegularExpression(@"^[0-9\s]{7,20}$", ErrorMessage = "El teléfono debe tener entre 7 y 15 dígitos")]
    string Phone,
    [param: Required(ErrorMessage = "El correo es obligatorio")]
    [param: EmailAddress(ErrorMessage = "El correo no tiene un formato válido")]
    string Email) : IRequest<WaitlistEntryDto>;

public sealed class RegisterWaitlistEntryCommandHandler(
    IWaitlistRepository entries,
    IEmailQueue emailQueue,
    ILogger<RegisterWaitlistEntryCommandHandler> logger) : IRequestHandler<RegisterWaitlistEntryCommand, WaitlistEntryDto>
{
    public async Task<WaitlistEntryDto> Handle(
        RegisterWaitlistEntryCommand command,
        CancellationToken cancellationToken)
    {
        Validate(command);

        WaitlistEntry entry;
        try
        {
            entry = new WaitlistEntry(command.FullName, command.CountryCode, command.Phone, command.Email);
        }
        catch (ArgumentException ex)
        {
            throw new ValidationFailedException(ex.Message);
        }

        await entries.AddAsync(entry, cancellationToken);

        EnqueueConfirmation(entry);

        return WaitlistEntryDto.From(entry);
    }

    private void EnqueueConfirmation(WaitlistEntry entry)
    {
        try
        {
            emailQueue.Enqueue(new EmailMessage(
                ToEmail: entry.Email,
                ToName: entry.FullName));
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "No se pudo encolar el correo de confirmación para {Email}; el registro ya quedó persistido.",
                entry.Email);
        }
    }

    private static void Validate(RegisterWaitlistEntryCommand command)
    {
        var fullName = command.FullName?.Trim() ?? string.Empty;
        if (fullName.Length is < WaitlistRules.MinNameLength or > WaitlistRules.MaxNameLength)
        {
            throw new ValidationFailedException($"El nombre completo debe tener entre {WaitlistRules.MinNameLength} y {WaitlistRules.MaxNameLength} caracteres.");
        }

        var phoneDigits = new string((command.Phone ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (phoneDigits.Length is < WaitlistRules.MinPhoneDigits or > WaitlistRules.MaxPhoneDigits)
        {
            throw new ValidationFailedException($"El teléfono debe tener entre {WaitlistRules.MinPhoneDigits} y {WaitlistRules.MaxPhoneDigits} dígitos.");
        }

        if (!IsValidEmail(command.Email?.Trim() ?? string.Empty))
        {
            throw new ValidationFailedException("El correo no tiene un formato válido.");
        }
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
