namespace TodoListApp2.Models.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "Employee"; // PlatformAdmin, OrgAdmin, Manager, Employee, User
    public string? Department { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<OrganizationMember> OrganizationMemberships { get; set; } = new List<OrganizationMember>();
    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; } = new List<WorkspaceMember>();
    public ICollection<TaskItem> AssignedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskItem> CreatedTasks { get; set; } = new List<TaskItem>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
