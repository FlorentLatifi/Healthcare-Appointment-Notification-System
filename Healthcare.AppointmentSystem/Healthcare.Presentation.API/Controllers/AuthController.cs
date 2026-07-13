using Asp.Versioning;
using Healthcare.Application.Commands.ForgotPassword;
using Healthcare.Application.Commands.ResetPassword;
using Healthcare.Application.Common;
using Healthcare.Application.Observability;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Application.Ports.Repositories;
using Healthcare.Domain.Entities;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Healthcare.Presentation.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace Healthcare.Presentation.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthController> _logger;
    private readonly ICommandHandler<ForgotPasswordCommand, Result> _forgotPasswordHandler;
    private readonly ICommandHandler<ResetPasswordCommand, Result> _resetPasswordHandler;
    private readonly IConfiguration _configuration;
    private readonly SecurityAuditWriter _securityAudit;
    private readonly IBusinessMetrics _metrics;

    public AuthController(
        IAuthenticationService authService,
        IUnitOfWork unitOfWork,
        JwtSettings jwtSettings,
        ILogger<AuthController> logger,
        ICommandHandler<ForgotPasswordCommand, Result> forgotPasswordHandler,
        ICommandHandler<ResetPasswordCommand, Result> resetPasswordHandler,
        IConfiguration configuration,
        SecurityAuditWriter securityAudit,
        IBusinessMetrics metrics)
    {
        _authService = authService;
        _unitOfWork = unitOfWork;
        _jwtSettings = jwtSettings;
        _logger = logger;
        _forgotPasswordHandler = forgotPasswordHandler;
        _resetPasswordHandler = resetPasswordHandler;
        _configuration = configuration;
        _securityAudit = securityAudit;
        _metrics = metrics;
    }

    private CookieOptions BuildRefreshCookieOptions(DateTimeOffset? expires = null)
    {
        // Secure only when the request is HTTPS so production keeps HttpOnly+Secure cookies,
        // while integration tests and local HTTP can still receive/send the refresh cookie.
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = expires,
        };
    }

    private void SetRefreshCookie(string refreshToken)
    {
        var expires = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationInDays);
        Response.Cookies.Append("refreshToken", refreshToken, BuildRefreshCookieOptions(expires));
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Append("refreshToken", "", BuildRefreshCookieOptions(DateTime.UtcNow.AddDays(-1)));
    }

    private static LoginResponse BuildLoginResponse(LoginResult result)
    {
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.AccessToken);

        return new LoginResponse
        {
            Token = result.AccessToken,
            ExpiresAt = result.ExpiresAt,
            Username = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "",
            Role = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "",
            PatientId = int.TryParse(jwtToken.Claims.FirstOrDefault(c => c.Type == "patient_id")?.Value, out var pid) ? pid : null,
            DoctorId = int.TryParse(jwtToken.Claims.FirstOrDefault(c => c.Type == "doctor_id")?.Value, out var did) ? did : null
        };
    }

    /// <summary>
    /// Request a password-reset email. Always returns the same success message
    /// (no account-enumeration). Rate-limited more strictly than login/register.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("PasswordResetPolicy")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        // Prefer SPA URL (e.g. http://localhost:5173/reset-password); never fall back to API host alone.
        var baseUrl = _configuration.GetValue<string>("App:ResetPasswordBaseUrl");
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var origin = Request.Headers.Origin.FirstOrDefault()
                ?? _configuration.GetValue<string>("AllowedOrigins:0");
            baseUrl = string.IsNullOrWhiteSpace(origin)
                ? "http://localhost:5173/reset-password"
                : $"{origin.TrimEnd('/')}/reset-password";
        }

        var command = new ForgotPasswordCommand
        {
            Email = request.Email?.Trim() ?? string.Empty,
            ResetLinkBaseUrl = baseUrl
        };

        var result = await _forgotPasswordHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            // Unexpected infrastructure failures only — never "email not found".
            return BadRequest(ApiResponse.ErrorResponse(
                result.Error,
                "Password reset request failed"));
        }

        _logger.LogInformation("Password reset requested (generic response returned)");
        return Ok(ApiResponse.SuccessResponse(
            "If the email address is registered, a password reset link has been sent."));
    }

    /// <summary>
    /// Complete password reset with email + single-use token + new strong password.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("PasswordResetPolicy")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordCommand
        {
            Email = request.Email?.Trim() ?? string.Empty,
            Token = request.Token?.Trim() ?? string.Empty,
            NewPassword = request.NewPassword ?? string.Empty
        };

        var result = await _resetPasswordHandler.HandleAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse.ErrorResponse(
                result.Error,
                "Password reset failed"));
        }

        _logger.LogInformation("Password reset completed successfully");
        return Ok(ApiResponse.SuccessResponse(
            "Password has been reset successfully. You can now sign in with your new password."));
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registering user: {Username}", request.Username);

        var result = await _authService.RegisterAsync(
            request.Username,
            request.Email,
            request.Password,
            request.Role,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Registration failed for {Username}: {Error}",
                request.Username, result.Error);
            return BadRequest(ApiResponse<int>.ErrorResponse(
                result.Error,
                "Registration failed"));
        }

        _logger.LogInformation("User {Username} registered successfully with ID {UserId}",
            request.Username, result.Value);

        return CreatedAtAction(
            nameof(GetCurrentUser),
            null,
            ApiResponse<int>.SuccessResponse(
                result.Value,
                "User registered successfully. Please login to get your token."));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        var result = await _authService.LoginAsync(
            request.Username,
            request.Password,
            cancellationToken);

        var clientIp = ClientIpResolver.GetClientIp(HttpContext);

        if (result.IsFailure)
        {
            _metrics.LoginFailed("invalid_credentials_or_inactive");
            _logger.LogWarning(
                "Login failed for {Username}: {Error} CorrelationId={CorrelationId}",
                request.Username, result.Error, CorrelationContext.Current);

            await _securityAudit.WriteAsync(
                "LoginFailed",
                "User",
                entityId: null,
                actorUserId: null,
                details: new
                {
                    username = request.Username,
                    reason = "invalid_credentials_or_inactive",
                    ip = clientIp,
                    userAgent = Request.Headers.UserAgent.ToString()
                },
                cancellationToken);

            return BadRequest(ApiResponse<LoginResponse>.ErrorResponse(
                result.Error,
                "Login failed"));
        }

        _metrics.LoginSucceeded();
        _logger.LogInformation(
            "User {Username} logged in successfully CorrelationId={CorrelationId}",
            request.Username,
            CorrelationContext.Current);

        var userIdClaim = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(result.Value.AccessToken)
            .Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        int? userId = int.TryParse(userIdClaim, out var parsedUserId) ? parsedUserId : null;

        if (userId.HasValue)
        {
            var session = new UserSession(
                userId.Value,
                result.Value.FamilyId,
                Request.Headers.UserAgent.ToString(),
                clientIp);
            await _unitOfWork.UserSessions.AddAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _securityAudit.WriteAsync(
                "LoginSucceeded",
                "User",
                entityId: userId,
                actorUserId: userId,
                details: new
                {
                    username = request.Username,
                    familyId = result.Value.FamilyId,
                    ip = clientIp,
                    userAgent = Request.Headers.UserAgent.ToString()
                },
                cancellationToken);
        }

        SetRefreshCookie(result.Value.RefreshToken);

        var response = BuildLoginResponse(result.Value);

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(
            response,
            "Login successful"));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token refresh requested");

        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return BadRequest(ApiResponse<LoginResponse>.ErrorResponse(
                "Refresh token cookie is missing.",
                "Token refresh failed"));
        }

        var result = await _authService.RefreshTokenAsync(
            refreshToken,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Token refresh failed: {Error}", result.Error);
            ClearRefreshCookie();
            return BadRequest(ApiResponse<LoginResponse>.ErrorResponse(
                result.Error,
                "Token refresh failed"));
        }

        var userIdClaim = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(result.Value.AccessToken)
            .Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userIdClaim, out var userId))
        {
            var sessions = await _unitOfWork.UserSessions.GetActiveByUserIdAsync(userId, cancellationToken);
            var session = sessions.FirstOrDefault(s => s.FamilyId == result.Value.FamilyId);
            if (session != null)
            {
                session.MarkUsed();
                await _unitOfWork.UserSessions.UpdateAsync(session, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        SetRefreshCookie(result.Value.RefreshToken);

        var response = BuildLoginResponse(result.Value);

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(
            response,
            "Token refreshed successfully"));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Logout requested");

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var refreshToken = Request.Cookies["refreshToken"];
        Guid? revokedFamilyId = null;

        // Revoke current refresh token AND its rotation family (blocks sibling tokens on this device).
        // Does NOT log out other devices — use DELETE /api/v1/sessions for that.
        if (!string.IsNullOrEmpty(refreshToken))
        {
            var result = await _authService.RevokeTokenAsync(refreshToken, cancellationToken);
            if (result.IsFailure)
            {
                _logger.LogWarning("Logout failed: {Error}", result.Error);
                return BadRequest(ApiResponse.ErrorResponse(result.Error, "Logout failed"));
            }

            revokedFamilyId = result.Value;
        }

        if (int.TryParse(userIdClaim, out var userId) && revokedFamilyId.HasValue)
        {
            var sessions = await _unitOfWork.UserSessions.GetActiveByUserIdAsync(userId, cancellationToken);
            var current = sessions.FirstOrDefault(s => s.FamilyId == revokedFamilyId.Value);
            if (current != null)
            {
                current.Revoke();
                await _unitOfWork.UserSessions.UpdateAsync(current, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        ClearRefreshCookie();

        if (int.TryParse(userIdClaim, out var auditUserId))
        {
            await _securityAudit.WriteAsync(
                "LogoutSucceeded",
                "User",
                entityId: auditUserId,
                actorUserId: auditUserId,
                details: new
                {
                    familyId = revokedFamilyId,
                    ip = ClientIpResolver.GetClientIp(HttpContext)
                },
                cancellationToken);
        }

        _logger.LogInformation("User logged out successfully (current session)");
        return Ok(ApiResponse.SuccessResponse(
            "Logout successful. This device session was revoked. Other devices remain signed in."));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        int? patientId = int.TryParse(User.FindFirst("patient_id")?.Value, out var pid) ? pid : null;
        int? doctorId = int.TryParse(User.FindFirst("doctor_id")?.Value, out var did) ? did : null;

        var userInfo = new
        {
            UserId = userId,
            Username = username,
            Email = email,
            Role = role,
            PatientId = patientId,
            DoctorId = doctorId
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            userInfo,
            "Current user information"));
    }
}
