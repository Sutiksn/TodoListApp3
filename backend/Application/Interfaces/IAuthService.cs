using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IAuthService
{
    Task<AuthResponse> SendRegistrationOtpAsync(SendRegistrationOtpRequest request);
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserProfileDto?> GetUserProfileAsync(int userId, string activeMode = "Personal");
    Task<AuthResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request);
    Task<AuthResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<AuthResponse> ResetPasswordWithOtpAsync(VerifyOtpAndResetRequest request);
    Task<List<UserProfileDto>> GetAllUsersAsync();
    Task<bool> DeleteAccountAsync(int userId);
}
