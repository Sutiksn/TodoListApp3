using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TasksController : ControllerBase
{
    private readonly ITaskService _taskService;

    public TasksController(ITaskService taskService)
    {
        _taskService = taskService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTasks(
        [FromQuery] int? workspaceId = null,
        [FromQuery] int? projectId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] int? assigneeUserId = null,
        [FromQuery] string? search = null)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var tasks = await _taskService.GetTasksAsync(userId, isPlatformAdmin, workspaceId, projectId, status, priority, assigneeUserId, search);
        return Ok(tasks);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTaskById(int id)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var task = await _taskService.GetTaskByIdAsync(id, userId, isPlatformAdmin);
        if (task == null) return NotFound(new { message = "Task not found." });
        return Ok(task);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request, [FromQuery] string mode = "Personal")
    {
        int userId = GetUserId();
        string username = GetUsername();

        try
        {
            var task = await _taskService.CreateTaskAsync(userId, username, request, mode);
            return CreatedAtAction(nameof(GetTaskById), new { id = task.Id }, task);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTask(int id, [FromBody] UpdateTaskRequest request)
    {
        int userId = GetUserId();
        string username = GetUsername();
        bool isPlatformAdmin = IsPlatformAdmin();

        try
        {
            var task = await _taskService.UpdateTaskAsync(userId, username, id, request, isPlatformAdmin);
            if (task == null) return NotFound(new { message = "Task not found." });
            return Ok(task);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateTaskStatusRequest request, [FromQuery] string mode = "Personal")
    {
        int userId = GetUserId();
        string username = GetUsername();
        bool isPlatformAdmin = IsPlatformAdmin();

        var task = await _taskService.UpdateTaskStatusAsync(userId, username, id, request.Status, mode, isPlatformAdmin);
        if (task == null) return NotFound(new { message = "Task not found." });
        return Ok(task);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTask(int id)
    {
        int userId = GetUserId();
        string username = GetUsername();
        bool isPlatformAdmin = IsPlatformAdmin();

        bool deleted = await _taskService.DeleteTaskAsync(userId, username, id, isPlatformAdmin);
        if (!deleted) return NotFound(new { message = "Task not found." });
        return Ok(new { message = "Task deleted successfully." });
    }

    [HttpPost("{id}/subtasks")]
    public async Task<IActionResult> AddSubtask(int id, [FromBody] CreateSubtaskRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var subtask = await _taskService.AddSubtaskAsync(id, userId, request, isPlatformAdmin);
        if (subtask == null) return NotFound(new { message = "Task not found." });
        return Ok(subtask);
    }

    [HttpPatch("subtasks/{subtaskId}/toggle")]
    public async Task<IActionResult> ToggleSubtask(int subtaskId)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var subtask = await _taskService.ToggleSubtaskAsync(subtaskId, userId, isPlatformAdmin);
        if (subtask == null) return NotFound(new { message = "Subtask not found." });
        return Ok(subtask);
    }

    [HttpDelete("subtasks/{subtaskId}")]
    public async Task<IActionResult> DeleteSubtask(int subtaskId)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        bool deleted = await _taskService.DeleteSubtaskAsync(subtaskId, userId, isPlatformAdmin);
        if (!deleted) return NotFound(new { message = "Subtask not found." });
        return Ok(new { message = "Subtask deleted successfully." });
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(int id, [FromBody] CreateCommentRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var comment = await _taskService.AddCommentAsync(userId, id, request, isPlatformAdmin);
        if (comment == null) return NotFound(new { message = "Task or User not found." });
        return Ok(comment);
    }

    [HttpPost("{id}/dependencies")]
    public async Task<IActionResult> AddDependency(int id, [FromBody] AddDependencyRequest request)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        var dep = await _taskService.AddDependencyAsync(id, userId, request, isPlatformAdmin);
        if (dep == null) return BadRequest(new { message = "Unable to add task dependency." });
        return Ok(dep);
    }

    [HttpDelete("dependencies/{dependencyId}")]
    public async Task<IActionResult> RemoveDependency(int dependencyId)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = IsPlatformAdmin();
        bool removed = await _taskService.RemoveDependencyAsync(dependencyId, userId, isPlatformAdmin);
        if (!removed) return NotFound(new { message = "Dependency not found." });
        return Ok(new { message = "Dependency removed successfully." });
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

    private bool IsPlatformAdmin()
    {
        return User.IsInRole("PlatformAdmin");
    }
}
