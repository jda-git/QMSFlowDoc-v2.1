using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Data;

namespace QMSFlowDoc.Web.Services;

public sealed record AuthenticatedUser(Guid Id, string Username, string FullName, IReadOnlyList<string> Roles);

public interface IWebAuthService
{
    Task<AuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
}

public sealed class WebAuthService(QmsFlowDocDbContext db) : IWebAuthService
{
    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var normalizedUsername = username.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(password))
            return null;

        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == normalizedUsername && u.IsActive, ct);

        if (user is null)
            return null;

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockedUntil = DateTime.UtcNow.AddMinutes(15);

            await db.SaveChangesAsync(ct);
            return null;
        }

        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var roles = user.Roles.Select(r => r.RoleName).OrderBy(r => r).ToArray();
        return new AuthenticatedUser(user.Id, user.Username, user.FullName, roles);
    }
}
