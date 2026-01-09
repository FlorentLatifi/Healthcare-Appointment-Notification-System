using Asp.Versioning;
using Healthcare.Application.Ports.Authentication;
using Healthcare.Presentation.API.Requests;
using Healthcare.Presentation.API.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Healthcare.Presentation.API.Controllers;

/// <summary>
/// Controller for authentication and authorization.
/// </summary>
/// <remarks>
/// REST Endpoints:
/// - POST /api/auth/register - Register new user
/// - POST /api/auth/login    - Login and get JWT token
/// - GET  /api/auth/me       - Get current user info (requires auth)
/// </remarks>
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

    /// <summary>
    /// Registers a new user.
    /// </summary>
    /// <param name="request">Registration details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User ID if successful.</returns>
    /// <response code="201">User registered successfully.</response>
    /// <response code="400">Invalid data or username/email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
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

    /// <summary>
    /// Authenticates a user and returns JWT token.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JWT token if successful.</returns>
    /// <response code="200">Login successful.</response>
    /// <response code="400">Invalid credentials.</response>
    [HttpPost("login")]
    [AllowAnonymous]
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

        // Parse token to extract claims
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(result.Value);

        var response = new LoginResponse
        {
            Token = result.Value,
            ExpiresAt = jwtToken.ValidTo,
            Username = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value ?? "",
            Role = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? ""
        };

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(
            response,
            "Login successful"));
    }

    /// <summary>
    /// Gets current authenticated user information.
    /// </summary>
    /// <returns>Current user details.</returns>
    /// <response code="200">User information retrieved.</response>
    /// <response code="401">Not authenticated.</response>
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