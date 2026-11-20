using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Models;
using TaskFlow.API.Settings;

namespace TaskFlow.API.Services;

public class AuthService
{
    private readonly ApplicationDbContext _context;
    private readonly JwtSettings _jwtSettings;

    public AuthService(ApplicationDbContext context)
        : this(context, new JwtSettings())
    {
    }

    public AuthService(ApplicationDbContext context, JwtSettings jwtSettings)
    {
        _context = context;
        _jwtSettings = jwtSettings;
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Fail("Username, email and password are required.");
        }

        if (request.Password.Length < 8)
        {
            return AuthResult.Fail("Password must be at least 8 characters long.");
        }

        var username = request.Username.Trim();
        var email = request.Email.Trim();

        var exists = await _context.Users.AnyAsync(u => u.Username == username || u.Email == email);
        if (exists)
        {
            return AuthResult.Conflict("Unable to register using the supplied details.");
        }

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return AuthResult.Success(user, "User registered successfully.", GenerateToken(user));
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Identifier) || string.IsNullOrWhiteSpace(request.Password))
        {
            return AuthResult.Fail("Username/email and password are required.");
        }

        var identifier = request.Identifier.Trim();

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Username == identifier || u.Email == identifier);

        if (user is null)
        {
            return AuthResult.Fail("Invalid username or password.");
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return AuthResult.Fail("Invalid username or password.");
        }

        var authenticatedUser = user ?? throw new InvalidOperationException("Authenticated user cannot be null.");
        return AuthResult.Success(authenticatedUser, "Login successful.", GenerateToken(authenticatedUser));
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _context.Users.FindAsync(userId);
    }

    private string GenerateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class AuthResult
{
    public bool IsSuccess { get; private set; }
    public bool IsConflict { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public User? User { get; private set; }
    public string? Token { get; private set; }

    public static AuthResult Success(User user, string message, string token)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new AuthResult
        {
            IsSuccess = true,
            Message = message,
            User = user,
            Token = token
        };
    }

    public static AuthResult Fail(string message)
    {
        return new AuthResult
        {
            IsSuccess = false,
            Message = message
        };
    }

    public static AuthResult Conflict(string message)
    {
        return new AuthResult
        {
            IsConflict = true,
            Message = message
        };
    }
}
