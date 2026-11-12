using Stockma.Domain.Entities;

namespace Stockma.Application.Waitlist;

public interface IWaitlistRepository
{
    Task AddAsync(WaitlistEntry entry, CancellationToken cancellationToken = default);
}
