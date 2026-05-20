namespace IAM.Domain.Interfaces;
using IAM.Domain.Entities;

public interface IUnitOfWork : IDisposable
{
    IUserRepository Users { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
     Task AddRefreshTokenAsync(RefreshToken token, CancellationToken ct = default);
}