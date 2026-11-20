using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.API.Data;
using TaskFlow.API.DTOs;
using TaskFlow.API.Services;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly ApplicationDbContext _context;

    public AuthController(AuthService authService, ApplicationDbContext context)
    {
        _authService = authService;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.IsSuccess || result.User is null || string.IsNullOrWhiteSpace(result.Token))
        {
            if (result.IsConflict)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Registration conflict",
                    detail: result.Message);
            }

            return ValidationError(result.Message);
        }

        return Ok(new AuthResponse
        {
            Token = result.Token,
            User = new UserSummaryDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Email = result.User.Email
            }
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (!result.IsSuccess || result.User is null || string.IsNullOrWhiteSpace(result.Token))
        {
            return ValidationError(result.Message);
        }

        return Ok(new AuthResponse
        {
            Token = result.Token,
            User = new UserSummaryDto
            {
                Id = result.User.Id,
                Username = result.User.Username,
                Email = result.User.Email
            }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserSummaryDto>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new UserSummaryDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email
        });
    }

    private ActionResult ValidationError(string message)
    {
        ModelState.AddModelError(string.Empty, message);
        return ValidationProblem();
    }
}
