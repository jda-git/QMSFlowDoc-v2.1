namespace QMSFlowDoc.Shared.DTOs;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, string Username, string FullName, List<string> Roles);

public record RegisterRequest(string Username, string Password, string FullName, string Email, string RoleName);

