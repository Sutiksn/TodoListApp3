namespace TodoListApp2.Models.Entities;

public class Workspace
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Mode { get; set; } = "Personal"; // Personal, Team, Project, Business, Admin
    public string Color { get; set; } = "#4f46e5";
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<WorkspaceMember> Members { get; set; } = new List<WorkspaceMember>();
    public ICollection<Goal> Goals { get; set; } = new List<Goal>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

public class WorkspaceMember
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Role { get; set; } = "Editor"; // Admin, Editor, Viewer
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
