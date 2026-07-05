using Asp.Versioning;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
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
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthenticationService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
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

        if (result.IsFailure)
        {
            _logger.LogWarning("Login failed for {Username}: {Error}",
                request.Username, result.Error);
            return BadRequest(ApiResponse<LoginResponse>.ErrorResponse(
                result.Error,
                "Login failed"));
        }

        _logger.LogInformation("User {Username} logged in successfully", request.Username);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Value.AccessToken);

        var response = new LoginResponse
        {
            Token = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            ExpiresAt = result.Value.ExpiresAt,
            Username = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "",
            Role = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? ""
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(
            response,
            "Login successful"));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Token refresh requested");

        var result = await _authService.RefreshTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Token refresh failed: {Error}", result.Error);
            return BadRequest(ApiResponse<LoginResponse>.ErrorResponse(
                result.Error,
                "Token refresh failed"));
        }

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Value.AccessToken);

        var response = new LoginResponse
        {
            Token = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            ExpiresAt = result.Value.ExpiresAt,
            Username = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "",
            Role = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? ""
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(
            response,
            "Token refreshed successfully"));
    }

    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Logout requested");

        var result = await _authService.RevokeTokenAsync(
            request.RefreshToken,
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning("Logout failed: {Error}", result.Error);
            return BadRequest(ApiResponse.ErrorResponse(
                result.Error,
                "Logout failed"));
        }

        _logger.LogInformation("User logged out successfully");
        return Ok(ApiResponse.SuccessResponse("Logout successful. Refresh token revoked."));
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

        var userInfo = new
        {
            UserId = userId,
            Username = username,
            Email = email,
            Role = role
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            userInfo,
            "Current user information"));
    }
}
