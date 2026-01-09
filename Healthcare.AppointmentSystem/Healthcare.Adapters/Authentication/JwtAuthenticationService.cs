using Healthcare.Application.Common;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Healthcare.Adapters.Authentication;

/// <summary>
/// JWT implementation of IAuthenticationService.
/// </summary>
/// <remarks>
/// Design Pattern: Adapter Pattern
/// 
/// This ADAPTER implements the PORT defined in Application layer.
/// It knows HOW to work with JWT, but Application layer doesn't care.
/// </remarks>
public sealed class JwtAuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<JwtAuthenticationService> _logger;

    public JwtAuthenticationService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        JwtSettings jwtSettings,
        ILogger<JwtAuthenticationService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings;
        _logger = logger;
    }

    public async Task<Result<int>> RegisterAsync(
        string username,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Check if user already exists
            var existingUser = await _unitOfWork.Users.GetByUsernameAsync(username, cancellationToken);
            if (existingUser != null)
            {
                return Result<int>.Failure($"Username '{username}' is already taken.");
            }

            var existingEmail = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);
            if (existingEmail != null)
            {
                return Result<int>.Failure($"Email '{email}' is already registered.");
            }

            // 2. Parse role
            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Result<int>.Failure($"Invalid role: {role}. Valid roles: Patient, Doctor, Admin");
            }

            // 3. Create value objects
            var emailVo = Email.Create(email);

            // 4. Hash password
            var passwordHash = _passwordHasher.HashPassword(password);

            // 5. Create user entity
            var user = User.Create(username, emailVo, passwordHash, userRole);

            // 6. Persist
            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {Username} registered successfully with role {Role}", username, role);

            return Result<int>.Success(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register user {Username}", username);
            return Result<int>.Failure($"Registration failed: {ex.Message}");
        }
    }

    public async Task<Result<string>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Find user
            var user = await _unitOfWork.Users.GetByUsernameAsync(username, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Login failed: User {Username} not found", username);
                return Result<string>.Failure("Invalid username or password.");
            }

            // 2. Verify password
            if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password for user {Username}", username);
                return Result<string>.Failure("Invalid username or password.");
            }

            // 3. Check if user is active
            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: User {Username} is deactivated", username);
                return Result<string>.Failure("Account is deactivated.");
            }

            // 4. Generate JWT token
            var token = GenerateJwtToken(user);

            _logger.LogInformation("User {Username} logged in successfully", username);

            return Result<string>.Success(token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user {Username}", username);
            return Result<string>.Failure($"Login failed: {ex.Message}");
        }
    }

    public async Task<Result<int>> ValidateTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out var userId))
            {
                return Result<int>.Failure("Invalid token: User ID not found.");
            }

            return Result<int>.Success(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token validation failed");
            return Result<int>.Failure($"Token validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a JWT token for the user.
    /// </summary>
    private string GenerateJwtToken(User user)
    {
        var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);
        var signingKey = new SymmetricSecurityKey(key);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email.Value),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes),
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}