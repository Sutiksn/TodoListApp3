using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkflowController : ControllerBase
{
    private readonly IWorkflowService _workflowService;
    private readonly IActivityService _activityService;

    public WorkflowController(IWorkflowService workflowService, IActivityService activityService)
    {
        _workflowService = workflowService;
        _activityService = activityService;
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivities([FromQuery] int? workspaceId = null, [FromQuery] int? projectId = null, [FromQuery] string? mode = null, [FromQuery] int limit = 20)
    {
        int userId = GetUserId();
        int? filterUserId = (mode != null && mode.Equals("Personal", StringComparison.OrdinalIgnoreCase)) ? userId : null;
        var logs = await _activityService.GetActivitiesAsync(workspaceId, projectId, filterUserId, mode, limit);
        return Ok(logs);
    }

    [HttpDelete("activity/clear")]
    public async Task<IActionResult> ClearActivities([FromQuery] int? workspaceId = null, [FromQuery] string? mode = null)
    {
        int count = await _activityService.ClearActivitiesAsync(workspaceId, mode);
        return Ok(new { message = $"Cleared {count} activity log entries successfully." });
    }

    [HttpGet("approvals")]
    public async Task<IActionResult> GetApprovals([FromQuery] int? orgId = null, [FromQuery] string? status = null)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        var approvals = await _workflowService.GetApprovalsAsync(orgId, status, userId, isPlatformAdmin);
        return Ok(approvals);
    }

    [HttpPost("approvals")]
    public async Task<IActionResult> RequestApproval([FromBody] CreateApprovalRequest request)
    {
        int userId = GetUserId();
        try
        {
            var approval = await _workflowService.RequestApprovalAsync(userId, request);
            return Ok(approval);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("approvals/{id}/decide")]
    public async Task<IActionResult> DecideApproval(int id, [FromBody] DecideApprovalRequest request)
    {
        int userId = GetUserId();
        string username = GetUsername();

        var approval = await _workflowService.DecideApprovalAsync(userId, username, id, request);
        if (approval == null) return NotFound(new { message = "Approval request not found." });
        return Ok(approval);
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }

    private string GetUsername()
    {
        return User.FindFirst(ClaimTypes.Name)?.Value ?? "user";
    }
}
