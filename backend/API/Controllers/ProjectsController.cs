using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet("goals")]
    public async Task<IActionResult> GetGoals([FromQuery] int workspaceId)
    {
        var goals = await _projectService.GetGoalsAsync(workspaceId);
        return Ok(goals);
    }

    [HttpPost("goals")]
    public async Task<IActionResult> CreateGoal([FromBody] CreateGoalRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        try
        {
            var goal = await _projectService.CreateGoalAsync(userId, request, isPlatformAdmin);
            return Ok(goal);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects([FromQuery] int workspaceId, [FromQuery] int? goalId = null)
    {
        var projects = await _projectService.GetProjectsAsync(workspaceId, goalId);
        return Ok(projects);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProjectById(int id)
    {
        var project = await _projectService.GetProjectByIdAsync(id);
        if (project == null) return NotFound(new { message = "Project not found." });
        return Ok(project);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        int userId = GetUserId();
        try
        {
            var project = await _projectService.CreateProjectAsync(userId, request);
            return Ok(project);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(int id, [FromBody] CreateProjectRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var project = await _projectService.UpdateProjectAsync(id, request, userId, isPlatformAdmin);
        if (project == null) return BadRequest(new { message = "Project not found or not authorized to update." });
        return Ok(project);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        bool deleted = await _projectService.DeleteProjectAsync(id, userId, isPlatformAdmin);
        if (!deleted) return BadRequest(new { message = "Project not found or you are not authorized to delete this project." });
        return Ok(new { message = "Project deleted successfully." });
    }

    [HttpGet("{id}/milestones")]
    public async Task<IActionResult> GetMilestones(int id)
    {
        var milestones = await _projectService.GetMilestonesAsync(id);
        return Ok(milestones);
    }

    [HttpPost("{id}/milestones")]
    public async Task<IActionResult> CreateMilestone(int id, [FromBody] CreateMilestoneRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        try
        {
            var milestone = await _projectService.CreateMilestoneAsync(request with { ProjectId = id }, userId, isPlatformAdmin);
            return Ok(milestone);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("milestones/{milestoneId}/toggle")]
    public async Task<IActionResult> ToggleMilestone(int milestoneId)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var milestone = await _projectService.ToggleMilestoneAsync(milestoneId, userId, isPlatformAdmin);
        if (milestone == null) return NotFound(new { message = "Milestone not found or not authorized to toggle." });
        return Ok(milestone);
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }

    private bool IsPlatformAdmin()
    {
        return User.IsInRole("PlatformAdmin");
    }
}
