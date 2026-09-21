using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IWorkspaceService
{
    Task<List<WorkspaceDto>> GetWorkspacesForUserAsync(int userId, string? mode = null);
    Task<WorkspaceDto?> GetWorkspaceByIdAsync(int workspaceId, int userId, bool isPlatformAdmin = false);
    Task<WorkspaceDto> CreateWorkspaceAsync(int userId, CreateWorkspaceRequest request);
    Task<WorkspaceDto?> UpdateWorkspaceAsync(int workspaceId, int userId, CreateWorkspaceRequest request, bool isPlatformAdmin = false);
    Task<bool> DeleteWorkspaceAsync(int workspaceId, int userId, bool isPlatformAdmin = false);
    Task<List<WorkspaceMemberDto>> GetWorkspaceMembersAsync(int workspaceId);
    Task<WorkspaceMemberDto?> AddWorkspaceMemberAsync(int workspaceId, AddWorkspaceMemberRequest request);
    Task<bool> RemoveWorkspaceMemberAsync(int workspaceId, int targetUserIdOrMemberId, int currentUserId, bool isPlatformAdmin = false);
}
