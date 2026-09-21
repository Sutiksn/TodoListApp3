using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("send-registration-otp")]
    public async Task<IActionResult> SendRegistrationOtp([FromBody] SendRegistrationOtpRequest request)
    {
        var response = await _authService.SendRegistrationOtpAsync(request);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var response = await _authService.RegisterAsync(request);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);
        if (!response.Success)
            return Unauthorized(response);

        return Ok(response);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser([FromQuery] string activeMode = "Personal")
    {
        int userId = GetUserId();
        var profile = await _authService.GetUserProfileAsync(userId, activeMode);
        if (profile == null)
            return NotFound(new { message = "User not found." });

        return Ok(profile);
    }

    [HttpPut("profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        int userId = GetUserId();
        var response = await _authService.UpdateProfileAsync(userId, request);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        int userId = GetUserId();
        var response = await _authService.ChangePasswordAsync(userId, request);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var response = await _authService.ForgotPasswordAsync(request);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] VerifyOtpAndResetRequest request)
    {
        var response = await _authService.ResetPasswordWithOtpAsync(request);
        if (!response.Success)
            return BadRequest(response);

        return Ok(response);
    }

    [HttpGet("users")]
    [Authorize]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpDelete("delete-account")]
    [Authorize]
    public async Task<IActionResult> DeleteMyAccount()
    {
        int userId = GetUserId();
        bool deleted = await _authService.DeleteAccountAsync(userId);
        if (!deleted)
            return BadRequest(new { message = "Cannot delete account (Platform Admin account cannot be deleted or user not found)." });

        return Ok(new { message = "Your account has been deleted successfully." });
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
