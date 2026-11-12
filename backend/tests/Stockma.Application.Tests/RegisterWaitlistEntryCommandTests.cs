using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Stockma.Application.Waitlist;
using Stockma.Application.Waitlist.Commands;
using Stockma.Application.Waitlist.Exceptions;
using Stockma.Domain.Entities;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class RegisterWaitlistEntryCommandTests
{
    private readonly FakeWaitlistRepository repository = new();
    private readonly FakeEmailQueue emailQueue = new();

    private RegisterWaitlistEntryCommandHandler CreateHandler() =>
        new(repository, emailQueue, NullLogger<RegisterWaitlistEntryCommandHandler>.Instance);

    private static RegisterWaitlistEntryCommand Command(
        string fullName = "Juan Pérez",
        string countryCode = "+57",
        string phone = "300 123 4567",
        string email = "Juan@Ejemplo.com") =>
        new(fullName, countryCode, phone, email);

    [Fact]
    public async Task Register_PersistsTheEntryWithNormalizedEmail()
    {
        var result = await CreateHandler().Handle(Command(), default);

        repository.Added.Should().ContainSingle();
        repository.Added[0].Email.Should().Be("juan@ejemplo.com", "la unicidad se evalúa sobre el correo normalizado");
        result.Id.Should().Be(repository.Added[0].Id);
        result.Email.Should().Be("juan@ejemplo.com", "el 201 devuelve el correo normalizado (contrato)");
    }

    [Fact]
    public async Task Register_EnqueuesAConfirmationEmailWithTheFullName()
    {
        await CreateHandler().Handle(Command(), default);

        emailQueue.Messages.Should().ContainSingle();
        var message = emailQueue.Messages[0];
        message.ToEmail.Should().Be("juan@ejemplo.com");
        message.ToName.Should().Be("Juan Pérez");
        message.Subject.Should().Contain("lista de espera");
        message.HtmlBody.Should().Contain("Juan Pérez", "el correo de confirmación es personalizado con el nombre");
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_IsRejected()
    {
        repository.EmailExists = true;

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        var ex = (await act.Should().ThrowAsync<WaitlistEmailAlreadyRegisteredException>()).Which;
        ex.ErrorCode.Should().Be(
            "WAITLIST_EMAIL_DUPLICATE",
            "el 409 del contrato sale del errorCode de la excepción de dominio");
        emailQueue.Messages.Should().BeEmpty("un registro rechazado no dispara correo");
        repository.Added.Should().BeEmpty();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("   ")]
    public async Task Register_WithAnInvalidEmail_IsRejected(string email)
    {
        Func<Task> act = () => CreateHandler().Handle(Command(email: email), default);

        var ex = (await act.Should().ThrowAsync<ValidationFailedException>()).Which;
        ex.ErrorCode.Should().Be("VALIDATION_FAILED");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefgh")]
    [InlineData("3001234567890123")]
    public async Task Register_WithAnInvalidPhone_IsRejected(string phone)
    {
        Func<Task> act = () => CreateHandler().Handle(Command(phone: phone), default);

        await act.Should().ThrowAsync<ValidationFailedException>();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    public async Task Register_WithAnInvalidFullName_IsRejected(string fullName)
    {
        Func<Task> act = () => CreateHandler().Handle(Command(fullName: fullName), default);

        await act.Should().ThrowAsync<ValidationFailedException>(
            "la validación de entrada es 400 VALIDATION_FAILED, nunca un 500 por ArgumentException");
    }

    [Fact]
    public async Task Register_WhenTheEmailEnqueueFails_StillPersistsTheEntry()
    {
        emailQueue.Throws = true;

        var result = await CreateHandler().Handle(Command(), default);

        result.Email.Should().Be("juan@ejemplo.com");
        repository.Added.Should().ContainSingle("el fallo del correo nunca es letal (spec 003)");
    }

    [Fact]
    public async Task Register_WithAnEmailLongerThanMax_IsRejectedAsValidationFailed()
    {
        Func<Task> act = () => CreateHandler().Handle(Command(email: new string('a', 257)), default);

        await act.Should().ThrowAsync<ValidationFailedException>(
            "un correo que excede el máximo debe ser 400 VALIDATION_FAILED, nunca un 500");
    }

    private sealed class FakeWaitlistRepository : IWaitlistRepository
    {
        public List<WaitlistEntry> Added { get; } = [];
        public bool EmailExists { get; set; }

        public Task AddAsync(WaitlistEntry entry, CancellationToken cancellationToken = default)
        {
            if (EmailExists)
            {
                throw new WaitlistEmailAlreadyRegisteredException(entry.Email);
            }

            Added.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEmailQueue : IEmailQueue
    {
        public List<EmailMessage> Messages { get; } = [];
        public bool Throws { get; set; }

        public void Enqueue(EmailMessage message)
        {
            if (Throws)
            {
                throw new InvalidOperationException("La cola de correo no está disponible.");
            }

            Messages.Add(message);
        }
    }
}
