namespace TodoListApp2.Models.DTOs;

public record TaskDto(
    int Id,
    int WorkspaceId,
    string WorkspaceName,
    int? ProjectId,
    string? ProjectName,
    string Title,
    string? Description,
    string Status, // Plan, ToDo, InProgress, Review, Completed
    string Priority, // Low, Medium, High, Urgent
    string Category,
    DateTime? StartDate,
    DateTime? DueDate,
    double EstimatedHours,
    double ActualHours,
    int CreatedByUserId,
    string CreatedByUsername,
    int? AssigneeUserId,
    string? AssigneeUsername,
    string? AssigneeFullName,
    bool IsCompleted,
    bool IsOverdue,
    int TotalSubtasks,
    int CompletedSubtasks,
    int SubtaskProgressPercentage,
    int CommentCount,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<SubtaskDto>? Subtasks = null,
    List<CommentDto>? Comments = null,
    List<TaskDependencyDto>? Dependencies = null
);

public record CreateTaskRequest(
    int WorkspaceId,
    string Title,
    string? Description = null,
    int? ProjectId = null,
    string Status = "ToDo",
    string Priority = "Medium",
    string Category = "General",
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    double EstimatedHours = 0,
    int? AssigneeUserId = null,
    List<string>? InitialSubtasks = null
);

public record UpdateTaskRequest(
    string Title,
    string? Description = null,
    int? ProjectId = null,
    string Status = "ToDo",
    string Priority = "Medium",
    string Category = "General",
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    double EstimatedHours = 0,
    double ActualHours = 0,
    int? AssigneeUserId = null
);

public record UpdateTaskStatusRequest(
    string Status // Plan, ToDo, InProgress, Review, Completed
);

public record SubtaskDto(
    int Id,
    int TaskItemId,
    string Title,
    bool IsCompleted,
    int OrderIndex,
    DateTime? CompletedAt
);

public record CreateSubtaskRequest(
    string Title
);

public record CommentDto(
    int Id,
    int TaskItemId,
    int UserId,
    string Username,
    string FullName,
    string Content,
    DateTime CreatedAt
);

public record CreateCommentRequest(
    string Content
);

public record TaskDependencyDto(
    int Id,
    int TaskItemId,
    int DependsOnTaskId,
    string DependsOnTaskTitle,
    string DependsOnTaskStatus,
    string DependencyType
);

public record AddDependencyRequest(
    int DependsOnTaskId,
    string DependencyType = "Blocks"
);
