using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;

namespace TodoListApp2.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendPasswordResetOtpAsync(string toEmail, string username, string otpCode)
    {
        return await SendOtpEmailAsync(
            toEmail,
            username,
            otpCode,
            "TaskFlow Password Recovery",
            "You requested to reset your TaskFlow account password. Use the 6-digit verification code below to complete your password reset:"
        );
    }

    public async Task<bool> SendRegistrationOtpAsync(string toEmail, string username, string otpCode)
    {
        return await SendOtpEmailAsync(
            toEmail,
            username,
            otpCode,
            "TaskFlow Email Verification",
            "Welcome to TaskFlow! Use the 6-digit verification code below to verify your email and activate your account:"
        );
    }

    public async Task<bool> SendPlatformAdminNotificationAsync(string adminEmail, string requesterName, string requesterEmail, string role)
    {
        var smtpHost = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var smtpPortStr = _configuration["Smtp:Port"] ?? "587";
        var smtpUser = (_configuration["Smtp:User"] ?? _configuration["Smtp:SenderEmail"] ?? "").Trim();
        var smtpPass = (_configuration["Smtp:Password"] ?? "").Trim().Replace(" ", "");
        var senderName = _configuration["Smtp:SenderName"] ?? "TaskFlow Security";
        var senderEmail = (_configuration["Smtp:SenderEmail"] ?? _configuration["Smtp:From"] ?? smtpUser).Trim();

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogWarning("[SMTP Warning] SMTP credentials not configured. Simulated Admin Notification to {Email} for {Requester} requesting {Role}", adminEmail, requesterName, role);
            return true;
        }

        int port = int.TryParse(smtpPortStr, out int p) ? p : 587;

        try
        {
            var message = new MimeMessage();
            message.Date = DateTimeOffset.UtcNow;
            message.MessageId = MimeUtils.GenerateMessageId("gmail.com");

            var fromBox = new MailboxAddress(senderName, senderEmail);
            message.From.Add(fromBox);
            message.Sender = fromBox;
            message.ReplyTo.Add(fromBox);
            message.To.Add(new MailboxAddress("Platform Administrator", adminEmail.Trim()));
            message.Subject = $"[Action Required] New {role} Role Request - {requesterName}";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"Hello Platform Administrator,\n\nA new user '{requesterName}' ({requesterEmail}) has registered and requested the '{role}' role.\n\nPlease log in to TaskFlow and check the Access & Requests panel to review and approve or reject this request.\n\nTaskFlow System",
                HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 20px; }}
  .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 12px; box-shadow: 0 4px 16px rgba(0,0,0,0.08); overflow: hidden; }}
  .header {{ background: linear-gradient(135deg, #0284c7 0%, #0369a1 100%); padding: 24px; text-align: center; color: #ffffff; }}
  .header h1 {{ margin: 0; font-size: 20px; font-weight: 700; }}
  .content {{ padding: 28px; color: #334155; line-height: 1.6; }}
  .badge {{ display: inline-block; padding: 6px 12px; background: #e0f2fe; color: #0369a1; border-radius: 6px; font-weight: bold; }}
  .footer {{ padding: 16px; text-align: center; font-size: 12px; color: #94a3b8; background: #f8fafc; border-top: 1px solid #e2e8f0; }}
</style>
</head>
<body>
  <div class='card'>
    <div class='header'>
      <h1>Role Request Notification</h1>
    </div>
    <div class='content'>
      <p>Hello <strong>Platform Administrator</strong>,</p>
      <p>A new user has submitted a request for administrative elevation:</p>
      <ul>
        <li><strong>Name:</strong> {requesterName}</li>
        <li><strong>Email:</strong> {requesterEmail}</li>
        <li><strong>Requested Role:</strong> <span class='badge'>{role}</span></li>
      </ul>
      <p>Please open your <strong>Access & Requests</strong> side panel in TaskFlow to review and approve or reject this request.</p>
    </div>
    <div class='footer'>
      &copy; {DateTime.UtcNow.Year} TaskFlow Enterprise Governance
    </div>
  </div>
</body>
</html>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.LocalDomain = "gmail.com";
            await client.ConnectAsync(smtpHost, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("[SMTP Success] Admin notification sent to {Email}", adminEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMTP Error] Failed to send admin notification to {Email}: {Message}", adminEmail, ex.Message);
            return false;
        }
    }

    private async Task<bool> SendOtpEmailAsync(string toEmail, string username, string otpCode, string subject, string description)
    {
        var smtpHost = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        var smtpPortStr = _configuration["Smtp:Port"] ?? "587";
        var smtpUser = (_configuration["Smtp:User"] ?? _configuration["Smtp:SenderEmail"] ?? "").Trim();
        var smtpPass = (_configuration["Smtp:Password"] ?? "").Trim().Replace(" ", "");
        var senderName = _configuration["Smtp:SenderName"] ?? "TaskFlow Security";
        var senderEmail = (_configuration["Smtp:SenderEmail"] ?? _configuration["Smtp:From"] ?? smtpUser).Trim();

        _logger.LogInformation("[OTP Service] Generated OTP for {Email} ({Username}): {Otp}", toEmail, username, otpCode);

        if (toEmail.EndsWith("@example.com", StringComparison.OrdinalIgnoreCase) || toEmail.EndsWith("@test.com", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[Test Simulated OTP] Simulated OTP for {Email}: {Otp}", toEmail, otpCode);
            return true;
        }

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
        {
            _logger.LogWarning("[SMTP Warning] SMTP credentials not configured. Simulated OTP for {Email}: {Otp}", toEmail, otpCode);
            return true;
        }

        int port = int.TryParse(smtpPortStr, out int p) ? p : 587;

        try
        {
            var message = new MimeMessage();
            message.Date = DateTimeOffset.UtcNow;
            message.MessageId = MimeUtils.GenerateMessageId("gmail.com");

            var fromBox = new MailboxAddress(senderName, senderEmail);
            message.From.Add(fromBox);
            message.Sender = fromBox;
            message.ReplyTo.Add(fromBox);
            message.To.Add(new MailboxAddress(username, toEmail.Trim()));
            message.Subject = $"{subject}: {otpCode}";

            var bodyBuilder = new BodyBuilder
            {
                TextBody = $"Hello {username},\n\n{description}\n\nYour 6-digit verification code is: {otpCode}\n\nThis code will expire in 10 minutes.\n\nTaskFlow Security",
                HtmlBody = $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #f8fafc; margin: 0; padding: 20px; }}
  .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 12px; box-shadow: 0 4px 16px rgba(0,0,0,0.08); overflow: hidden; }}
  .header {{ background: linear-gradient(135deg, #4f46e5 0%, #7c3aed 100%); padding: 24px; text-align: center; color: #ffffff; }}
  .header h1 {{ margin: 0; font-size: 22px; font-weight: 700; }}
  .content {{ padding: 28px; color: #334155; line-height: 1.6; }}
  .otp-box {{ background: #f1f5f9; border: 2px dashed #6366f1; border-radius: 8px; text-align: center; padding: 16px; margin: 20px 0; }}
  .otp-code {{ font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #4338ca; }}
  .footer {{ padding: 16px; text-align: center; font-size: 12px; color: #94a3b8; background: #f8fafc; border-top: 1px solid #e2e8f0; }}
</style>
</head>
<body>
  <div class='card'>
    <div class='header'>
      <h1>TaskFlow Security</h1>
    </div>
    <div class='content'>
      <p>Hello <strong>{username}</strong>,</p>
      <p>{description}</p>
      
      <div class='otp-box'>
        <div class='otp-code'>{otpCode}</div>
      </div>
      
      <p style='font-size: 13px; color: #64748b;'>This code will expire in <strong>10 minutes</strong>. If you did not initiate this request, you can safely ignore this email.</p>
    </div>
    <div class='footer'>
      &copy; {DateTime.UtcNow.Year} TaskFlow Application. All rights reserved.
    </div>
  </div>
</body>
</html>"
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            client.LocalDomain = "gmail.com";
            await client.ConnectAsync(smtpHost, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("[SMTP Success] OTP email sent successfully to {Email} (Code: {Otp})", toEmail, otpCode);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SMTP Error] Failed to send OTP email to {Email}: {Message}", toEmail, ex.Message);
            return false;
        }
    }
}
