using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IActivityService
{
    Task LogAsync(int? organizationId, int? workspaceId, int? projectId, int? taskId, int userId, string username, string action, string details, string mode = "Personal");
    Task<List<ActivityLogDto>> GetActivitiesAsync(int? workspaceId = null, int? projectId = null, int? userId = null, string? mode = null, int limit = 50, bool excludePersonal = false);
    Task<int> ClearActivitiesAsync(int? workspaceId = null, string? mode = null);
}
