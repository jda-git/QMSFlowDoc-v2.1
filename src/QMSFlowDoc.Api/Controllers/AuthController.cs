using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QMSFlowDoc.Api.Data;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace QMSFlowDoc.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    [HttpGet("needs-bootstrap")]
    public async Task<ActionResult<bool>> NeedsBootstrap()
    {
        return Ok(!await _context.Users.AnyAsync());
    }

    [HttpPost("bootstrap")]
    public async Task<ActionResult> Bootstrap(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync())
        {
            return BadRequest("El sistema ya ha sido inicializado.");
        }

        // Ensure roles exist
        if (!await _context.Roles.AnyAsync())
        {
            var roles = new List<Role>
            {
                new Role { Id = Guid.NewGuid(), RoleName = "Administrador", Description = "Acceso total" },
                new Role { Id = Guid.NewGuid(), RoleName = "Consultor", Description = "Solo lectura" },
                new Role { Id = Guid.NewGuid(), RoleName = "Staff", Description = "Acceso básico" }
            };
            _context.Roles.AddRange(roles);
            await _context.SaveChangesAsync();
        }

        var adminRole = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Administrador");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            Roles = adminRole != null ? new List<Role> { adminRole } : new List<Role>()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Administrador creado correctamente." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        Console.WriteLine($"[*] Intento de login para: {request.Username}");
        
        var user = await _context.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null)
        {
            Console.WriteLine("[!] Usuario no encontrado.");
            return Unauthorized("Usuario o contraseña incorrectos.");
        }

        if (!VerifyPassword(request.Password, user.PasswordHash))
        {
            Console.WriteLine("[!] Contraseña incorrecta (Hash mismatch).");
            return Unauthorized("Usuario o contraseña incorrectos.");
        }

        Console.WriteLine("[+] Login exitoso.");
        var token = GenerateJwtToken(user);

        return Ok(new LoginResponse(
            token,
            user.Username,
            user.FullName,
            user.Roles.Select(r => r.RoleName).ToList()
        ));
    }

    [HttpPost("register")]
    public async Task<ActionResult> Register(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(u => u.Username == request.Username))
        {
            return BadRequest("El nombre de usuario ya existe.");
        }

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == request.RoleName);
        if (role == null)
        {
            // If requested role doesn't exist, try to find a default or return error
            role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == "Consultor");
            if (role == null)
            {
                // Last resort: create it if it's a standard one or assign first available
                role = await _context.Roles.FirstOrDefaultAsync();
            }
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            Roles = role != null ? new List<Role> { role } : new List<Role>()
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Ok(new { Id = user.Id, Username = user.Username });
    }

    [Authorize]
    [HttpDelete("purge-users")]
    public async Task<ActionResult> PurgeUsers()
    {
        // Delete all related records first to avoid FK issues
        _context.StaffAuthorizations.RemoveRange(_context.StaffAuthorizations);
        _context.StaffCompetencyStatuses.RemoveRange(_context.StaffCompetencyStatuses);
        _context.CompetencyEvaluations.RemoveRange(_context.CompetencyEvaluations);
        _context.StaffTrainings.RemoveRange(_context.StaffTrainings);
        _context.StaffProfiles.RemoveRange(_context.StaffProfiles);
        
        // Delete all users except 'admin'
        var nonAdminUsers = _context.Users.Where(u => u.Username != "admin");
        _context.Users.RemoveRange(nonAdminUsers);
        
        await _context.SaveChangesAsync();
        return Ok("Registros de personal y usuarios eliminados correctamente (excepto administrador).");
    }

    private string GenerateJwtToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        foreach (var role in user.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.RoleName));
        }

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.Now.AddMinutes(double.Parse(jwtSettings["ExpiryMinutes"]!)),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Simplistic hashing for MVP - use a proper library like BCrypt in production
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}
