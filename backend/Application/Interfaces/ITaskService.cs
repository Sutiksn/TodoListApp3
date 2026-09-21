using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface ITaskService
{
    Task<List<TaskDto>> GetTasksAsync(int userId, bool isPlatformAdmin, int? workspaceId = null, int? projectId = null, string? status = null, string? priority = null, int? assigneeUserId = null, string? search = null);
    Task<TaskDto?> GetTaskByIdAsync(int taskId, int userId, bool isPlatformAdmin = false);
    Task<TaskDto> CreateTaskAsync(int userId, string username, CreateTaskRequest request, string mode = "Personal");
    Task<TaskDto?> UpdateTaskAsync(int userId, string username, int taskId, UpdateTaskRequest request, bool isPlatformAdmin = false);
    Task<TaskDto?> UpdateTaskStatusAsync(int userId, string username, int taskId, string newStatus, string mode = "Personal", bool isPlatformAdmin = false);
    Task<bool> DeleteTaskAsync(int userId, string username, int taskId, bool isPlatformAdmin = false);
    Task<SubtaskDto?> AddSubtaskAsync(int taskId, int userId, CreateSubtaskRequest request, bool isPlatformAdmin = false);
    Task<SubtaskDto?> ToggleSubtaskAsync(int subtaskId, int userId, bool isPlatformAdmin = false);
    Task<bool> DeleteSubtaskAsync(int subtaskId, int userId, bool isPlatformAdmin = false);
    Task<CommentDto?> AddCommentAsync(int userId, int taskId, CreateCommentRequest request, bool isPlatformAdmin = false);
    Task<TaskDependencyDto?> AddDependencyAsync(int taskId, int userId, AddDependencyRequest request, bool isPlatformAdmin = false);
    Task<bool> RemoveDependencyAsync(int dependencyId, int userId, bool isPlatformAdmin = false);
}
