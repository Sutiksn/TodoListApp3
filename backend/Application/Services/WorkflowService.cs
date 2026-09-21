using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class WorkflowService : IWorkflowService
{
    private readonly AppDbContext _context;
    private readonly IActivityService _activityService;
    private readonly INotificationService _notificationService;

    public WorkflowService(
        AppDbContext context,
        IActivityService activityService,
        INotificationService notificationService)
    {
        _context = context;
        _activityService = activityService;
        _notificationService = notificationService;
    }

    public async Task<List<WorkflowApprovalDto>> GetApprovalsAsync(int? orgId = null, string? status = null, int userId = 0, bool isPlatformAdmin = false)
    {
        var query = _context.WorkflowApprovals
            .Include(a => a.TaskItem)
                .ThenInclude(t => t.Workspace)
            .Include(a => a.RequesterUser)
            .Include(a => a.ApproverUser)
            .AsQueryable();

        query = query.Where(a => a.TaskItem.Workspace.Mode != "Personal");

        if (userId > 0 && !isPlatformAdmin)
        {
            var userOrgIds = await _context.OrganizationMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.OrganizationId)
                .ToListAsync();

            if (userOrgIds.Count == 0)
            {
                return new List<WorkflowApprovalDto>();
            }

            if (orgId.HasValue && orgId.Value > 0)
            {
                if (!userOrgIds.Contains(orgId.Value))
                {
                    return new List<WorkflowApprovalDto>();
                }
                query = query.Where(a => a.TaskItem.Workspace.OrganizationId == orgId.Value);
            }
            else
            {
                query = query.Where(a => a.TaskItem.Workspace.OrganizationId.HasValue && userOrgIds.Contains(a.TaskItem.Workspace.OrganizationId.Value));
            }
        }
        else if (orgId.HasValue && orgId.Value > 0)
        {
            query = query.Where(a => a.TaskItem.Workspace.OrganizationId == orgId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status);

        var list = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        return list.Select(a => new WorkflowApprovalDto(
            a.Id,
            a.TaskItemId,
            a.TaskItem.Title,
            a.TaskItem.Status,
            a.RequesterUserId,
            a.RequesterUser.Username,
            a.RequesterUser.FullName,
            a.ApproverUserId,
            a.ApproverUser?.Username,
            a.Status,
            a.Comments,
            a.CreatedAt,
            a.DecidedAt
        )).ToList();
    }

    public async Task<WorkflowApprovalDto> RequestApprovalAsync(int userId, CreateApprovalRequest request)
    {
        var task = await _context.Tasks.Include(t => t.Workspace).FirstOrDefaultAsync(t => t.Id == request.TaskItemId);
        if (task == null) throw new ArgumentException("Task not found.");

        task.Status = "Review";
        task.UpdatedAt = DateTime.UtcNow;

        var approval = new WorkflowApproval
        {
            TaskItemId = task.Id,
            RequesterUserId = userId,
            ApproverUserId = request.SpecificApproverUserId,
            Status = "Pending",
            Comments = request.Comments,
            CreatedAt = DateTime.UtcNow
        };

        _context.WorkflowApprovals.Add(approval);
        await _context.SaveChangesAsync();

        var requester = await _context.Users.FindAsync(userId);

        if (request.SpecificApproverUserId.HasValue)
        {
            await _notificationService.CreateNotificationAsync(
                request.SpecificApproverUserId.Value,
                "Workflow Approval Requested",
                $"{requester?.FullName ?? "User"} requested your approval on '{task.Title}'.",
                $"/tasks/{task.Id}",
                "ApprovalRequested"
            );
        }

        await _activityService.LogAsync(
            task.Workspace.OrganizationId,
            task.WorkspaceId,
            task.ProjectId,
            task.Id,
            userId,
            requester?.Username ?? "user",
            "ApprovalRequested",
            $"Submitted task '{task.Title}' for approval: {request.Comments}",
            "Business"
        );

        return (await GetApprovalsAsync(task.Workspace.OrganizationId, null)).First(a => a.Id == approval.Id);
    }

    public async Task<WorkflowApprovalDto?> DecideApprovalAsync(int approverUserId, string approverUsername, int approvalId, DecideApprovalRequest request)
    {
        var approval = await _context.WorkflowApprovals
            .Include(a => a.TaskItem)
                .ThenInclude(t => t.Workspace)
            .Include(a => a.RequesterUser)
            .FirstOrDefaultAsync(a => a.Id == approvalId);

        if (approval == null) return null;

        approval.Status = request.Status; // Approved or Rejected
        approval.ApproverUserId = approverUserId;
        approval.DecidedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Comments))
        {
            approval.Comments = $"{approval.Comments} | Decision: {request.Comments}";
        }

        // If approved, advance task to Completed; if rejected, return to InProgress
        if (request.Status == "Approved")
        {
            approval.TaskItem.Status = "Completed";
            approval.TaskItem.CompletedAt = DateTime.UtcNow;
        }
        else if (request.Status == "Rejected")
        {
            approval.TaskItem.Status = "InProgress";
        }
        approval.TaskItem.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _notificationService.CreateNotificationAsync(
            approval.RequesterUserId,
            $"Task {request.Status}",
            $"Your task '{approval.TaskItem.Title}' was {request.Status.ToLower()} by {approverUsername}.",
            $"/tasks/{approval.TaskItem.Id}",
            "ApprovalDecided"
        );

        await _activityService.LogAsync(
            approval.TaskItem.Workspace.OrganizationId,
            approval.TaskItem.WorkspaceId,
            approval.TaskItem.ProjectId,
            approval.TaskItem.Id,
            approverUserId,
            approverUsername,
            $"Approval{request.Status}",
            $"Task '{approval.TaskItem.Title}' was {request.Status.ToLower()}",
            "Business"
        );

        return (await GetApprovalsAsync(approval.TaskItem.Workspace.OrganizationId, null)).FirstOrDefault(a => a.Id == approvalId);
    }
}
