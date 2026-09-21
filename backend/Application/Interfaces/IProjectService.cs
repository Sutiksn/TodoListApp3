using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IProjectService
{
    Task<List<GoalDto>> GetGoalsAsync(int workspaceId);
    Task<GoalDto> CreateGoalAsync(int userId, CreateGoalRequest request, bool isPlatformAdmin = false);
    Task<List<ProjectDto>> GetProjectsAsync(int workspaceId, int? goalId = null);
    Task<ProjectDto?> GetProjectByIdAsync(int projectId);
    Task<ProjectDto> CreateProjectAsync(int userId, CreateProjectRequest request);
    Task<ProjectDto?> UpdateProjectAsync(int projectId, CreateProjectRequest request, int userId = 0, bool isPlatformAdmin = false);
    Task<bool> DeleteProjectAsync(int projectId, int userId, bool isPlatformAdmin = false);
    Task<List<MilestoneDto>> GetMilestonesAsync(int projectId);
    Task<MilestoneDto> CreateMilestoneAsync(CreateMilestoneRequest request, int userId = 0, bool isPlatformAdmin = false);
    Task<MilestoneDto?> ToggleMilestoneAsync(int milestoneId, int userId = 0, bool isPlatformAdmin = false);
}
