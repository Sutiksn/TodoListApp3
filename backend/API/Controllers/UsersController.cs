using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser([FromQuery] string activeMode = "Personal")
    {
        int userId = GetUserId();
        var profile = await _authService.GetUserProfileAsync(userId, activeMode);
        if (profile == null)
            return NotFound(new { message = "User not found." });

        return Ok(profile);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
