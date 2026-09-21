namespace TodoListApp2.Models.Entities;

public class TaskItem
{
    public int Id { get; set; }
    public int WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;
    public int? ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Core Workflow Stages: Plan -> ToDo -> InProgress -> Review -> Completed
    public string Status { get; set; } = "ToDo"; // Plan, ToDo, InProgress, Review, Completed
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public string Category { get; set; } = "General";
    
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public double EstimatedHours { get; set; } = 0;
    public double ActualHours { get; set; } = 0;

    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public int? AssigneeUserId { get; set; }
    public User? AssigneeUser { get; set; }

    public bool IsCompleted => Status == "Completed";
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskAttachment> Attachments { get; set; } = new List<TaskAttachment>();
    public ICollection<TaskDependency> Dependencies { get; set; } = new List<TaskDependency>();
    public ICollection<WorkflowApproval> Approvals { get; set; } = new List<WorkflowApproval>();
}

public class Subtask
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; } = false;
    public int OrderIndex { get; set; } = 0;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TaskComment
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class TaskAttachment
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; } = 0;
    public string ContentType { get; set; } = "application/octet-stream";
    public int UploadedByUserId { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class TaskDependency
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;
    public int DependsOnTaskId { get; set; }
    public TaskItem DependsOnTask { get; set; } = null!;
    public string DependencyType { get; set; } = "Blocks"; // Blocks, BlockedBy
}

public class WorkflowApproval
{
    public int Id { get; set; }
    public int TaskItemId { get; set; }
    public TaskItem TaskItem { get; set; } = null!;
    public int RequesterUserId { get; set; }
    public User RequesterUser { get; set; } = null!;
    public int? ApproverUserId { get; set; }
    public User? ApproverUser { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? Comments { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
}
