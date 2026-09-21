namespace TodoListApp2.Models.DTOs;

public record WorkflowApprovalDto(
    int Id,
    int TaskItemId,
    string TaskTitle,
    string TaskStatus,
    int RequesterUserId,
    string RequesterUsername,
    string RequesterFullName,
    int? ApproverUserId,
    string? ApproverUsername,
    string Status, // Pending, Approved, Rejected
    string? Comments,
    DateTime CreatedAt,
    DateTime? DecidedAt
);

public record CreateApprovalRequest(
    int TaskItemId,
    string? Comments = null,
    int? SpecificApproverUserId = null
);

public record DecideApprovalRequest(
    string Status, // Approved, Rejected
    string? Comments = null
);
