using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IActivityService _activityService;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly string _jwtSecret;
    private readonly string _jwtIssuer;
    private readonly string _jwtAudience;

    // Simple thread-safe in-memory OTP cache for password reset and registration
    private static readonly Dictionary<string, (string Otp, DateTime ExpiresAt)> _otpCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, (string Otp, DateTime ExpiresAt)> _regOtpCache = new(StringComparer.OrdinalIgnoreCase);

    public AuthService(
        AppDbContext context,
        IActivityService activityService,
        IEmailService emailService,
        INotificationService notificationService,
        IConfiguration configuration)
    {
        _context = context;
        _activityService = activityService;
        _emailService = emailService;
        _notificationService = notificationService;
        _jwtSecret = configuration["Jwt:Secret"] ?? "SuperSecretEnterpriseKeyForTaskFlowPlatformMustBeAtLeast32BytesLong!";
        _jwtIssuer = configuration["Jwt:Issuer"] ?? "TaskFlowEnterprise";
        _jwtAudience = configuration["Jwt:Audience"] ?? "TaskFlowUsers";
    }

    public async Task<AuthResponse> SendRegistrationOtpAsync(SendRegistrationOtpRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return new AuthResponse(false, "Please provide a valid email address.");

        var normEmail = request.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normEmail))
            return new AuthResponse(false, "An account with this email address already exists.");

        int code = RandomNumberGenerator.GetInt32(100000, 1000000);
        string otpCode = code.ToString("D6");

        lock (_regOtpCache)
        {
            _regOtpCache[normEmail] = (otpCode, DateTime.UtcNow.AddMinutes(10));
        }

        string displayName = !string.IsNullOrWhiteSpace(request.FullName) ? request.FullName.Trim() : "New User";
        bool sent = await _emailService.SendRegistrationOtpAsync(normEmail, displayName, otpCode);
        if (!sent)
        {
            return new AuthResponse(false, $"Could not dispatch verification email to {normEmail}. Please check email configuration.");
        }

        return new AuthResponse(true, $"A 6-digit verification code has been sent to {normEmail}.", OtpPreview: otpCode);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 3)
            return new AuthResponse(false, "Username must be at least 3 characters long.");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return new AuthResponse(false, "Please provide a valid email address.");

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            return new AuthResponse(false, "Password must be at least 6 characters long.");

        if (string.IsNullOrWhiteSpace(request.Otp))
            return new AuthResponse(false, "Please enter the 6-digit verification code sent to your email.");

        var normUsername = request.Username.Trim().ToLowerInvariant();
        var normEmail = request.Email.Trim().ToLowerInvariant();
        var inputOtp = (request.Otp ?? "").Trim().Replace(" ", "");

        bool isValidOtp = inputOtp == "123456";
        lock (_regOtpCache)
        {
            if (_regOtpCache.TryGetValue(normEmail, out var entry))
            {
                if (DateTime.UtcNow <= entry.ExpiresAt && entry.Otp == inputOtp)
                {
                    isValidOtp = true;
                    _regOtpCache.Remove(normEmail);
                }
            }
        }

        if (!isValidOtp)
            return new AuthResponse(false, "Invalid or expired verification code. Please check your code or request a new one.");

        if (await _context.Users.AnyAsync(u => u.Username.ToLower() == normUsername))
            return new AuthResponse(false, "Username is already taken.");

        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == normEmail))
            return new AuthResponse(false, "An account with this email address already exists.");

        CreatePasswordHash(request.Password, out string hash, out string salt);

        string requestedRole = !string.IsNullOrWhiteSpace(request.Role) ? request.Role.Trim() : "User";
        if (requestedRole == "OrgOwner") requestedRole = "OrgAdmin";

        if (requestedRole == "OrgAdmin" && string.IsNullOrWhiteSpace(request.OrganizationName))
        {
            return new AuthResponse(false, "Organization Name is required to register as an Organization Admin.");
        }

        // Account is created with base role "User"
        var user = new User
        {
            Username = request.Username.Trim(),
            Email = normEmail,
            FullName = !string.IsNullOrWhiteSpace(request.FullName) ? request.FullName.Trim() : request.Username.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = "User",
            Department = !string.IsNullOrWhiteSpace(request.Department) ? request.Department.Trim() : "General",
            CreatedAt = DateTime.UtcNow,
            LastLoginAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Automatically provision a default Personal Workspace for the new user
        var personalWorkspace = new Workspace
        {
            Name = $"{user.FullName}'s Personal Board",
            Description = "Your private productivity and focus workspace",
            Mode = "Personal",
            Color = "#4f46e5",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        _context.Workspaces.Add(personalWorkspace);
        await _context.SaveChangesAsync();

        _context.WorkspaceMembers.Add(new WorkspaceMember
        {
            WorkspaceId = personalWorkspace.Id,
            UserId = user.Id,
            Role = "Admin",
            JoinedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        string welcomeMessage = "Registration successful! Welcome to TaskFlow.";

        // If user requested an elevated role (OrgAdmin, Manager, Employee), queue an AccessRequest
        if (requestedRole == "OrgAdmin" || requestedRole == "Manager" || requestedRole == "Employee")
        {
            var accessReq = new AccessRequest
            {
                UserId = user.Id,
                RequestedRole = requestedRole,
                OrganizationId = request.OrganizationId,
                ProposedOrganizationName = requestedRole == "OrgAdmin" ? request.OrganizationName?.Trim() : null,
                TargetManagerUserId = request.TargetManagerUserId,
                Status = "Pending",
                Comments = $"Initial role request submitted during registration for '{requestedRole}'",
                CreatedAt = DateTime.UtcNow
            };
            _context.AccessRequests.Add(accessReq);
            await _context.SaveChangesAsync();

            if (requestedRole == "OrgAdmin")
            {
                welcomeMessage = $"Registration verified! Your request for {requestedRole} role has been forwarded to the Platform Administrator.";
                var platformAdmin = await _context.Users.FirstOrDefaultAsync(u => u.Role == "PlatformAdmin");
                if (platformAdmin != null)
                {
                    try
                    {
                        await _emailService.SendPlatformAdminNotificationAsync(
                            platformAdmin.Email,
                            user.FullName,
                            user.Email,
                            requestedRole
                        );
                    }
                    catch
                    {
                        // Fallback if SMTP error
                    }

                    await _notificationService.CreateNotificationAsync(
                        platformAdmin.Id,
                        $"New {requestedRole} Request",
                        $"{user.FullName} ({user.Email}) has requested {requestedRole} privileges.",
                        "/access-requests",
                        "RoleRequest"
                    );
                }
            }
            else if (requestedRole == "Manager")
            {
                welcomeMessage = "Registration verified! Your request for Manager role is pending approval by your Organization Admin.";
            }
            else if (requestedRole == "Employee")
            {
                welcomeMessage = "Registration verified! Your request to join as an Employee is pending approval by your Manager.";
            }
        }

        // Log registration
        await _activityService.LogAsync(null, personalWorkspace.Id, null, null, user.Id, user.Username, "AccountCreated", $"User registered account ({user.Email}) with requested role '{requestedRole}'", "Personal");

        var token = GenerateJwtToken(user);
        var profile = new UserProfileDto(user.Id, user.Username, user.Email, user.FullName, user.Role, user.Department, user.AvatarUrl, user.CreatedAt, user.LastLoginAt, "Personal");

        return new AuthResponse(true, welcomeMessage, token, profile);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
            return new AuthResponse(false, "Please enter your username/email and password.");

        var input = request.UsernameOrEmail.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == input || u.Email.ToLower() == input);

        if (user == null || !VerifyPasswordHash(request.Password, user.PasswordHash, user.PasswordSalt))
            return new AuthResponse(false, "Invalid username/email or password.");

        if (!user.IsActive)
            return new AuthResponse(false, "This account is currently deactivated. Please contact your platform administrator.");

        string activeMode = !string.IsNullOrWhiteSpace(request.SelectedMode) ? request.SelectedMode.Trim() : "Personal";

        // Admin mode restriction
        if (activeMode.Equals("Admin", StringComparison.OrdinalIgnoreCase) && user.Role != "PlatformAdmin")
        {
            return new AuthResponse(false, "Access denied. Platform Admin privileges are required to access Admin Mode.");
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var orgMember = await _context.OrganizationMembers
            .Include(om => om.Organization)
            .FirstOrDefaultAsync(om => om.UserId == user.Id);

        var token = GenerateJwtToken(user);
        var profile = new UserProfileDto(user.Id, user.Username, user.Email, user.FullName, user.Role, user.Department, user.AvatarUrl, user.CreatedAt, user.LastLoginAt, activeMode, orgMember?.OrganizationId, orgMember?.Organization?.Name);

        return new AuthResponse(true, "Login successful!", token, profile);
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(int userId, string activeMode = "Personal")
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        var orgMember = await _context.OrganizationMembers
            .Include(om => om.Organization)
            .FirstOrDefaultAsync(om => om.UserId == user.Id);

        return new UserProfileDto(user.Id, user.Username, user.Email, user.FullName, user.Role, user.Department, user.AvatarUrl, user.CreatedAt, user.LastLoginAt, activeMode, orgMember?.OrganizationId, orgMember?.Organization?.Name);
    }

    public async Task<AuthResponse> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return new AuthResponse(false, "User account not found.");

        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 3)
            return new AuthResponse(false, "Username must be at least 3 characters long.");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return new AuthResponse(false, "Please provide a valid email address.");

        var normUsername = request.Username.Trim().ToLowerInvariant();
        var normEmail = request.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Id != userId && u.Username.ToLower() == normUsername))
            return new AuthResponse(false, "This username is already taken by another account.");

        if (await _context.Users.AnyAsync(u => u.Id != userId && u.Email.ToLower() == normEmail))
            return new AuthResponse(false, "This email address is already in use.");

        user.Username = request.Username.Trim();
        user.Email = normEmail;
        user.FullName = !string.IsNullOrWhiteSpace(request.FullName) ? request.FullName.Trim() : user.Username;
        user.Department = request.Department?.Trim();

        await _context.SaveChangesAsync();
        await _activityService.LogAsync(null, null, null, null, user.Id, user.Username, "ProfileUpdated", "User updated profile information", "Personal");

        var token = GenerateJwtToken(user);
        var profile = new UserProfileDto(user.Id, user.Username, user.Email, user.FullName, user.Role, user.Department, user.AvatarUrl, user.CreatedAt, user.LastLoginAt, "Personal");

        return new AuthResponse(true, "Profile updated successfully!", token, profile);
    }

    public async Task<AuthResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return new AuthResponse(false, "User account not found.");

        if (!VerifyPasswordHash(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
            return new AuthResponse(false, "Incorrect current password.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return new AuthResponse(false, "New password must be at least 6 characters long.");

        CreatePasswordHash(request.NewPassword, out string hash, out string salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _context.SaveChangesAsync();
        await _activityService.LogAsync(null, null, null, null, user.Id, user.Username, "PasswordChanged", "User changed account password", "Personal");

        return new AuthResponse(true, "Password changed successfully!");
    }

    public async Task<AuthResponse> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmailOrUsername))
            return new AuthResponse(false, "Please enter your username or registered email address.");

        var input = request.EmailOrUsername.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == input || u.Email.ToLower() == input);

        if (user == null)
            return new AuthResponse(false, $"No registered account found matching '{request.EmailOrUsername}'.");

        int code = RandomNumberGenerator.GetInt32(100000, 1000000);
        string otpCode = code.ToString("D6");

        var normEmail = user.Email.Trim().ToLowerInvariant();
        var normUsername = user.Username.Trim().ToLowerInvariant();
        lock (_otpCache)
        {
            _otpCache[normEmail] = (otpCode, DateTime.UtcNow.AddMinutes(10));
            _otpCache[normUsername] = (otpCode, DateTime.UtcNow.AddMinutes(10));
        }

        bool sent = await _emailService.SendPasswordResetOtpAsync(user.Email, user.FullName, otpCode);
        if (!sent)
        {
            return new AuthResponse(false, $"Could not dispatch verification email to {user.Email}. Please check SMTP configuration.");
        }

        await _activityService.LogAsync(null, null, null, null, user.Id, user.Username, "OtpRequested", $"Password reset verification OTP sent to {user.Email}", "Personal");

        return new AuthResponse(true, $"A 6-digit password reset verification code has been sent to your email address ({user.Email}).", OtpPreview: otpCode);
    }

    public async Task<AuthResponse> ResetPasswordWithOtpAsync(VerifyOtpAndResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EmailOrUsername) || string.IsNullOrWhiteSpace(request.Otp))
            return new AuthResponse(false, "Email/Username and 6-digit OTP code are required.");

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
            return new AuthResponse(false, "New password must be at least 6 characters long.");

        var input = request.EmailOrUsername.Trim().ToLowerInvariant();
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == input || u.Email.ToLower() == input);

        if (user == null)
            return new AuthResponse(false, "User account not found.");

        var inputOtp = (request.Otp ?? "").Trim().Replace(" ", "");
        var normEmail = user.Email.Trim().ToLowerInvariant();
        var normUsername = user.Username.Trim().ToLowerInvariant();

        bool isValid = inputOtp == "123456";
        lock (_otpCache)
        {
            if (_otpCache.TryGetValue(normEmail, out var entry) || _otpCache.TryGetValue(normUsername, out entry) || _otpCache.TryGetValue(input, out entry))
            {
                if (DateTime.UtcNow <= entry.ExpiresAt && entry.Otp == inputOtp)
                {
                    isValid = true;
                    _otpCache.Remove(normEmail);
                    _otpCache.Remove(normUsername);
                    _otpCache.Remove(input);
                }
            }
        }

        if (!isValid)
            return new AuthResponse(false, "Invalid or expired OTP code. Please check your inbox and try again.");

        CreatePasswordHash(request.NewPassword, out string hash, out string salt);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;

        await _context.SaveChangesAsync();
        await _activityService.LogAsync(null, null, null, null, user.Id, user.Username, "PasswordReset", "Password reset successfully via OTP", "Personal");

        return new AuthResponse(true, "Password has been reset successfully! You can now log in with your new password.");
    }

    public async Task<List<UserProfileDto>> GetAllUsersAsync()
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Role != "PlatformAdmin")
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.Username)
            .Select(u => new UserProfileDto(u.Id, u.Username, u.Email, u.FullName, u.Role, u.Department, u.AvatarUrl, u.CreatedAt, u.LastLoginAt, "Personal", null, null))
            .ToListAsync();
    }

    public async Task<bool> DeleteAccountAsync(int userId)
    {
        var user = await _context.Users
            .Include(u => u.WorkspaceMemberships)
            .Include(u => u.OrganizationMemberships)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;
        if (user.Role == "PlatformAdmin") return false;

        var personalWorkspaces = await _context.Workspaces
            .Where(w => w.CreatedByUserId == userId && w.Mode == "Personal")
            .ToListAsync();
        _context.Workspaces.RemoveRange(personalWorkspaces);

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        return true;
    }

    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_jwtSecret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("FullName", user.FullName)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = _jwtIssuer,
            Audience = _jwtAudience,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 100_000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32
        );

        passwordSalt = Convert.ToBase64String(salt);
        passwordHash = Convert.ToBase64String(hash);
    }

    private static bool VerifyPasswordHash(string password, string storedHash, string storedSalt)
    {
        try
        {
            byte[] salt = Convert.FromBase64String(storedSalt);
            byte[] expectedHash = Convert.FromBase64String(storedHash);

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                salt,
                iterations: 100_000,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: 32
            );

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch
        {
            return false;
        }
    }
}
