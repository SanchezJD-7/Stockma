using Stockma.Domain.Entities;

namespace Stockma.Application.Waitlist;

public sealed record WaitlistEntryDto(Guid Id, string Email)
{
    public static WaitlistEntryDto From(WaitlistEntry entry) => new(entry.Id, entry.Email);
}
