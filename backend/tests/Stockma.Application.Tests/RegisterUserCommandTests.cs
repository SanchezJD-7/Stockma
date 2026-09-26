using FluentAssertions;
using Stockma.Application.Identity;
using Stockma.Application.Identity.Commands;
using Stockma.Domain.Exceptions;

namespace Stockma.Application.Tests;

public class RegisterUserCommandTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private const string Email = "beto@droga.co";
    private const string Password = "S3gura#2026";
    private const string PhoneNumber = "+573001234567";

    private readonly FakeUserAccounts accounts = new();
    private readonly FakeTenantContext tenantContext = new(TenantId);

    private RegisterUserCommandHandler CreateHandler() => new(accounts, tenantContext);

    private static RegisterUserCommand Command(
        string phoneNumber = PhoneNumber,
        string role = TenantRoles.Member) => new(Email, Password, phoneNumber, role);

    [Fact]
    public async Task Register_CreatesTheUserInTheCallersTenant()
    {
        var result = await CreateHandler().Handle(Command(), default);

        accounts.Created.Should().NotBeNull();
        accounts.Created!.TenantId
            .Should()
            .Be(TenantId, "FR-005: el usuario nace atado al tenant del header, no a uno que venga en el body");
        result.UserId.Should().Be(accounts.CreatedId);
    }

    [Fact]
    public async Task Register_RequiresThePhoneNumberInTheSameAct()
    {
        Func<Task> act = () => CreateHandler().Handle(Command(phoneNumber: "   "), default);

        await act.Should()
            .ThrowAsync<ArgumentException>(
                "T064: un usuario creado sin celular no puede entrar desde un dispositivo no trusted "
                + "y queda inservible hasta que un admin lo complete");
    }

    [Fact]
    public async Task Register_PassesThePhoneNumberThrough()
    {
        await CreateHandler().Handle(Command(), default);

        accounts.Created!.PhoneNumber.Should().Be(PhoneNumber);
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_IsRejected()
    {
        accounts.EmailTaken = true;

        var exception = await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => CreateHandler().Handle(Command(), default));

        exception.ErrorCode.Should().Be("AUTH_EMAIL_DUPLICATE");
    }

    [Fact]
    public async Task Register_WithADuplicateEmail_ChecksAcrossTheWholePlatform()
    {
        accounts.EmailTaken = true;

        Func<Task> act = () => CreateHandler().Handle(Command(), default);

        await act.Should()
            .ThrowAsync<EmailAlreadyRegisteredException>(
                "ADR-001: el email es único en TODA la plataforma, no por tenant");

        accounts.Created.Should().BeNull();
    }

    [Fact]
    public async Task Register_WithAnUnknownRole_IsRejected()
    {
        Func<Task> act = () => CreateHandler().Handle(Command(role: "SuperAdmin"), default);

        await act.Should()
            .ThrowAsync<ArgumentException>(
                "ADR-005: el admin de plataforma no es un rol de tenant; sólo TenantAdmin y Member");
    }

    [Theory]
    [InlineData(TenantRoles.TenantAdmin)]
    [InlineData(TenantRoles.Member)]
    public async Task Register_AcceptsTheTwoTenantRoles(string role)
    {
        await CreateHandler().Handle(Command(role: role), default);

        accounts.Created!.Role.Should().Be(role);
    }

    [Fact]
    public async Task Register_TrimsTheEmail()
    {
        await new RegisterUserCommandHandler(accounts, tenantContext)
            .Handle(new RegisterUserCommand($"  {Email}  ", Password, PhoneNumber, TenantRoles.Member), default);

        accounts.Created!.Email.Should().Be(Email);
    }
}
