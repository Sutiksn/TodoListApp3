using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IWorkflowService
{
    Task<List<WorkflowApprovalDto>> GetApprovalsAsync(int? orgId = null, string? status = null, int userId = 0, bool isPlatformAdmin = false);
    Task<WorkflowApprovalDto> RequestApprovalAsync(int userId, CreateApprovalRequest request);
    Task<WorkflowApprovalDto?> DecideApprovalAsync(int approverUserId, string approverUsername, int approvalId, DecideApprovalRequest request);
}
