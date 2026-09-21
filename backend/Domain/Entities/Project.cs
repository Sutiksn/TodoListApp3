namespace TodoListApp2.Models.Entities;

public class Goal
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Status { get; set; } = "InProgress"; // NotStarted, InProgress, Achieved, AtRisk
    public int ProgressPercentage { get; set; } = 0;
    public DateTime? TargetDate { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Project> Projects { get; set; } = new List<Project>();
}

public class Project
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public int? GoalId { get; set; }
    public Goal? Goal { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#6366f1";
    public string Status { get; set; } = "Active"; // Planning, Active, InReview, Completed, OnHold
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int ProgressPercentage { get; set; } = 0;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Milestone> Milestones { get; set; } = new List<Milestone>();
    public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();
}

public class Milestone
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsReached { get; set; } = false;
    public DateTime? ReachedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
