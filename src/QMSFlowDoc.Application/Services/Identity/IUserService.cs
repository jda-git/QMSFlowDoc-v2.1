using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Application.Services.Identity
{
    public record UserDto(Guid Id, string UserName, string Email, string FullName, string Role, bool IsActive, DateTime CreatedAt);
    public record CreateUserRequest(string UserName, string Email, string FullName, string Password, string Role);
    public record UpdateUserRequest(string Email, string FullName, string Role, bool IsActive);
    public record RoleDto(Guid Id, string Name, string? Description);

    public interface IUserService
    {
        Task<List<UserDto>> GetUsersAsync();
        Task<bool> CreateUserAsync(CreateUserRequest request);
        Task<bool> UpdateUserAsync(Guid id, UpdateUserRequest request);
        Task<bool> ResetPasswordAsync(Guid id, string newPassword);
        Task<List<RoleDto>> GetRolesAsync();
        Task<bool> CreateRoleAsync(string name, string? description);
    }
}
