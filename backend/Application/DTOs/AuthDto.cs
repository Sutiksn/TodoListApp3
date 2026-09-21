namespace TodoListApp2.Models.DTOs;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string? FullName = null,
    string? Role = "User",
    string? Department = "General",
    string? Otp = null,
    int? OrganizationId = null,
    int? TargetManagerUserId = null,
    string? OrganizationName = null
);

public record SendRegistrationOtpRequest(
    string Email,
    string? FullName = null
);

public record LoginRequest(
    string UsernameOrEmail,
    string Password,
    string? SelectedMode = "Personal" // Personal, Team, Project, Business, Admin
);

public record UserProfileDto(
    int Id,
    string Username,
    string Email,
    string FullName,
    string Role,
    string? Department,
    string? AvatarUrl,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    string ActiveMode = "Personal",
    int? OrganizationId = null,
    string? OrganizationName = null
);

public record UpdateProfileRequest(
    string Username,
    string Email,
    string FullName,
    string? Department
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record ForgotPasswordRequest(
    string EmailOrUsername
);

public record VerifyOtpAndResetRequest(
    string EmailOrUsername,
    string Otp,
    string NewPassword
);

public record AuthResponse(
    bool Success,
    string Message,
    string? Token = null,
    UserProfileDto? User = null,
    string? OtpPreview = null
);
