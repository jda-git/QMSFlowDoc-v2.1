namespace QMSFlowDoc.Shared.Models;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Role> Roles { get; set; } = new();
}

public class Role
{
    public Guid Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Permission> Permissions { get; set; } = new();
}

public class Permission
{
    public Guid Id { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public string? Description { get; set; }
}
