using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkspacesController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspacesController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetWorkspaces([FromQuery] string? mode = null)
    {
        int userId = GetUserId();
        var workspaces = await _workspaceService.GetWorkspacesForUserAsync(userId, mode);
        return Ok(workspaces);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetWorkspaceById(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin") || User.IsInRole("OrgAdmin");
        var ws = await _workspaceService.GetWorkspaceByIdAsync(id, userId, isPlatformAdmin);
        if (ws == null) return NotFound(new { message = "Workspace not found or access denied." });
        return Ok(ws);
    }

    [HttpPost]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        int userId = GetUserId();
        try
        {
            var ws = await _workspaceService.CreateWorkspaceAsync(userId, request);
            return CreatedAtAction(nameof(GetWorkspaceById), new { id = ws.Id }, ws);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateWorkspace(int id, [FromBody] CreateWorkspaceRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin") || User.IsInRole("OrgAdmin");
        var ws = await _workspaceService.UpdateWorkspaceAsync(id, userId, request, isPlatformAdmin);
        if (ws == null) return NotFound(new { message = "Workspace not found or access denied." });
        return Ok(ws);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWorkspace(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin") || User.IsInRole("OrgAdmin");
        bool deleted = await _workspaceService.DeleteWorkspaceAsync(id, userId, isPlatformAdmin);
        if (!deleted) return BadRequest(new { message = "Workspace not found or you are not authorized to delete this workspace." });
        return Ok(new { message = "Workspace deleted successfully." });
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(int id)
    {
        var members = await _workspaceService.GetWorkspaceMembersAsync(id);
        return Ok(members);
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(int id, [FromBody] AddWorkspaceMemberRequest request)
    {
        try
        {
            var member = await _workspaceService.AddWorkspaceMemberAsync(id, request);
            if (member == null) return NotFound(new { message = "User not found to add to workspace." });
            return Ok(member);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}/members/{memberId}")]
    public async Task<IActionResult> RemoveMember(int id, int memberId)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin") || User.IsInRole("OrgAdmin");
        bool removed = await _workspaceService.RemoveWorkspaceMemberAsync(id, memberId, userId, isPlatformAdmin);
        if (!removed) return BadRequest(new { message = "Member not found or you are not authorized to remove this member." });
        return Ok(new { message = "Member removed successfully." });
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
