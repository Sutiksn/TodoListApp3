namespace TodoListApp2.Services;

public interface IEmailService
{
    Task<bool> SendPasswordResetOtpAsync(string toEmail, string username, string otpCode);
    Task<bool> SendRegistrationOtpAsync(string toEmail, string username, string otpCode);
    Task<bool> SendPlatformAdminNotificationAsync(string adminEmail, string requesterName, string requesterEmail, string role);
}
