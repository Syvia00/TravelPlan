using Microsoft.EntityFrameworkCore;
using TravelPlan.Api.Data;
using TravelPlan.Shared.Models;

namespace TravelPlan.Api.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(TravelPlanDbContext context) : base(context)
    {
    }

    public Task<User?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        DbSet.SingleOrDefaultAsync(u => u.Email != null && u.Email.ToLower() == email.ToLower(), cancellationToken);

    public async Task<User> FindOrCreatePendingAsync(string email, CancellationToken cancellationToken = default)
    {
        var existing = await FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = email,
            DisplayName = email,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await AddAsync(user, cancellationToken);
        await SaveChangesAsync(cancellationToken);

        return user;
    }
}
