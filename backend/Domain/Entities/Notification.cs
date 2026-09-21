namespace TodoListApp2.Models.Entities;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string Type { get; set; } = "System"; // TaskAssigned, StatusChanged, ApprovalRequested, ApprovalDecided, CommentAdded, DeadlineApproaching, System
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ActivityLog
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public int? WorkspaceId { get; set; }
    public int? ProjectId { get; set; }
    public int? TaskId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public string Mode { get; set; } = "Personal"; // Personal, Team, Project, Business, Admin
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
