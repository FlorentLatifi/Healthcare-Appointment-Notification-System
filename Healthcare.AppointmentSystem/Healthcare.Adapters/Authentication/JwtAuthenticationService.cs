using Healthcare.Application.Common;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Domain.Enums;
using Healthcare.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Healthcare.Adapters.Authentication;

public sealed class JwtAuthenticationService : IAuthenticationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<JwtAuthenticationService> _logger;
    private readonly IDatabase? _redisDb;
    private readonly ConcurrentDictionary<string, string> _memoryStore;

    private const string RefreshTokenKeyPrefix = "refresh_token:";

    public JwtAuthenticationService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        JwtSettings jwtSettings,
        ILogger<JwtAuthenticationService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings;
        _logger = logger;
        _redisDb = redis?.GetDatabase();
        _memoryStore = new ConcurrentDictionary<string, string>();
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

            if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            {
                return Result<int>.Failure($"Invalid role: {role}. Valid roles: Patient, Doctor, Admin");
            }

            var emailVo = Email.Create(email);
            var passwordHash = _passwordHasher.HashPassword(password);
            var user = User.Create(username, emailVo, passwordHash, userRole);

            await _unitOfWork.Users.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {Username} registered successfully with role {Role}", username, role);

            return Result<int>.Success(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register user {Username}", username);
            throw;
        }
    }

    public async Task<Result<LoginResult>> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByUsernameAsync(username, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("Login failed: User {Username} not found", username);
                return Result<LoginResult>.Failure("Invalid username or password.");
            }

            if (!_passwordHasher.VerifyPassword(password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: Invalid password for user {Username}", username);
                return Result<LoginResult>.Failure("Invalid username or password.");
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: User {Username} is deactivated", username);
                return Result<LoginResult>.Failure("Account is deactivated.");
            }

            var accessToken = GenerateJwtToken(user);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes);
            var refreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id.ToString());

            _logger.LogInformation("User {Username} logged in successfully", username);

            return Result<LoginResult>.Success(new LoginResult
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user {Username}", username);
            throw;
        }
    }

    public async Task<Result<LoginResult>> RefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHash = HashToken(refreshToken);
            var userId = await ConsumeRefreshTokenAsync(tokenHash);

            if (userId == null)
            {
                _logger.LogWarning("Refresh token reuse or invalid token attempted");
                return Result<LoginResult>.Failure("Invalid or expired refresh token.");
            }

            if (!int.TryParse(userId, out var parsedUserId))
            {
                return Result<LoginResult>.Failure("Invalid refresh token data.");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(parsedUserId, cancellationToken);
            if (user == null || !user.IsActive)
            {
                _logger.LogWarning("Refresh failed: User {UserId} not found or inactive", userId);
                return Result<LoginResult>.Failure("User account not found or deactivated.");
            }

            var accessToken = GenerateJwtToken(user);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes);
            var newRefreshToken = await GenerateAndStoreRefreshTokenAsync(user.Id.ToString());

            _logger.LogInformation("Token refreshed for user {Username}", user.Username);

            return Result<LoginResult>.Success(new LoginResult
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = expiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh token failed");
            throw;
        }
    }

    public async Task<Result> RevokeTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var tokenHash = HashToken(refreshToken);
            await DeleteRefreshTokenAsync(tokenHash);

            _logger.LogInformation("Refresh token revoked");
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke refresh token");
            throw;
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
            throw;
        }
    }

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

    private static string GenerateRefreshTokenValue()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<string> GenerateAndStoreRefreshTokenAsync(string userId)
    {
        var refreshToken = GenerateRefreshTokenValue();
        var tokenHash = HashToken(refreshToken);
        var ttl = TimeSpan.FromDays(_jwtSettings.RefreshTokenExpirationInDays);

        if (_redisDb != null)
        {
            try
            {
                await _redisDb.StringSetAsync(
                    $"{RefreshTokenKeyPrefix}{tokenHash}",
                    userId,
                    ttl);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis unavailable for refresh-token storage");
                throw;
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis unavailable for refresh-token storage");
                throw;
            }
        }
        else
        {
            _memoryStore.TryAdd(tokenHash, userId);
            _ = ScheduleMemoryCleanup(tokenHash, ttl);
        }

        return refreshToken;
    }

    private async Task<string?> ConsumeRefreshTokenAsync(string tokenHash)
    {
        if (_redisDb != null)
        {
            try
            {
                var script = @"
                    local val = redis.call('GET', KEYS[1])
                    if val then
                        redis.call('DEL', KEYS[1])
                        return val
                    end
                    return nil";

                var key = $"{RefreshTokenKeyPrefix}{tokenHash}";
                var result = await _redisDb.ScriptEvaluateAsync(script, new RedisKey[] { key });
                return result.IsNull ? null : (string?)result;
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis unavailable for refresh-token storage");
                throw;
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis unavailable for refresh-token storage");
                throw;
            }
        }

        if (_memoryStore.TryRemove(tokenHash, out var userId))
        {
            return userId;
        }

        return null;
    }

    private async Task DeleteRefreshTokenAsync(string tokenHash)
    {
        if (_redisDb != null)
        {
            try
            {
                var key = $"{RefreshTokenKeyPrefix}{tokenHash}";
                await _redisDb.KeyDeleteAsync(key);
            }
            catch (RedisConnectionException ex)
            {
                _logger.LogError(ex, "Redis unavailable for refresh-token storage");
                throw;
            }
            catch (RedisTimeoutException ex)
            {
                _logger.LogError(ex, "Redis unavailable for refresh-token storage");
                throw;
            }
        }
        else
        {
            _memoryStore.TryRemove(tokenHash, out _);
        }
    }

    private async Task ScheduleMemoryCleanup(string tokenHash, TimeSpan ttl)
    {
        await Task.Delay(ttl);
        _memoryStore.TryRemove(tokenHash, out _);
    }
}
