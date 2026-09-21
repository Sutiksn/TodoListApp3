namespace TodoListApp2.Models.Entities;

public enum UserRole
{
    PlatformAdmin = 1,
    OrgAdmin = 2,
    Manager = 3,
    Employee = 4,
    Member = 5,
    Guest = 6
}

public enum WorkspaceMode
{
    Personal = 1,
    Team = 2,
    Project = 3,
    Business = 4,
    Admin = 5
}

public enum WorkspaceMemberRole
{
    Admin = 1,
    Editor = 2,
    Viewer = 3
}

public enum GoalStatus
{
    NotStarted = 1,
    InProgress = 2,
    Achieved = 3,
    AtRisk = 4
}

public enum ProjectStatus
{
    Planning = 1,
    Active = 2,
    InReview = 3,
    Completed = 4,
    OnHold = 5
}

public enum TaskStatusStage
{
    Plan = 1,
    ToDo = 2,
    InProgress = 3,
    Review = 4,
    Completed = 5
}

public enum TaskPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Urgent = 4
}

public enum ApprovalStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum NotificationType
{
    TaskAssigned = 1,
    StatusChanged = 2,
    ApprovalRequested = 3,
    ApprovalDecided = 4,
    CommentAdded = 5,
    DeadlineApproaching = 6,
    System = 7
}
