using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IAnalyticsService
{
    Task<PersonalAnalyticsDto> GetPersonalAnalyticsAsync(int userId);
    Task<TeamAnalyticsDto> GetTeamAnalyticsAsync(int workspaceId);
    Task<ProjectAnalyticsDto> GetProjectAnalyticsAsync(int workspaceId);
    Task<BusinessAnalyticsDto> GetBusinessAnalyticsAsync(int orgId, int userId = 0, bool isPlatformAdmin = false);
    Task<AdminPlatformAnalyticsDto> GetAdminPlatformAnalyticsAsync();
}
