using BCrypt.Net;
using CreditRiskScoring.Api.DTOs;
using CreditRiskScoring.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditRiskScoring.Api.Controllers;

/// <summary>
/// Faux authentication — stores credentials in memory for demo purposes.
/// Replace with EF Core user store + Identity in production.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController(JwtService jwtService) : ControllerBase
{
    // In-memory user store (demo only)
    private static readonly Dictionary<string, (string Id, string Email, string Hash, string Role)> Users = new();

    /// <summary>Register a new user account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Register([FromBody] RegisterRequest request)
    {
        if (Users.ContainsKey(request.Username))
            return Conflict(new MessageResponse("Username already taken."));

        string hash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12);
        Users[request.Username] = (Guid.NewGuid().ToString(), request.Email, hash, "User");

        return StatusCode(201, new MessageResponse("Registered successfully. You can now log in."));
    }

    /// <summary>Log in and receive a Bearer JWT.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (!Users.TryGetValue(request.Username, out var user))
            return Unauthorized(new MessageResponse("Invalid credentials."));

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Hash))
            return Unauthorized(new MessageResponse("Invalid credentials."));

        string token = jwtService.GenerateToken(user.Id, request.Username, user.Role);
        return Ok(new AuthResponse(token, "Bearer", jwtService.ExpirySeconds, user.Id, request.Username));
    }
}
