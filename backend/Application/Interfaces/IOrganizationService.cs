using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IOrganizationService
{
    Task<List<OrganizationDto>> GetOrganizationsForUserAsync(int userId);
    Task<OrganizationDto?> GetOrganizationByIdAsync(int orgId, int userId = 0, bool isPlatformAdmin = false);
    Task<OrganizationDto> CreateOrganizationAsync(int userId, CreateOrganizationRequest request);
    Task<WorkspaceDto> RegisterOrganizationWorkspaceAsync(int orgId, int currentUserId);
    Task<List<OrgMemberDto>> GetOrganizationMembersAsync(int orgId, int currentUserId = 0, bool isPlatformAdmin = false);
    Task<OrgMemberDto?> AddOrganizationMemberAsync(int orgId, AddOrgMemberRequest request, int currentUserId, bool isPlatformAdmin = false);
    Task<OrgMemberDto?> UpdateOrganizationMemberAsync(int orgId, int memberId, UpdateOrgMemberRequest request, int currentUserId, bool isPlatformAdmin = false);
    Task<bool> RemoveOrganizationMemberAsync(int orgId, int memberId, int currentUserId, bool isPlatformAdmin = false);
    Task<bool> DeleteOrganizationAsync(int orgId, int currentUserId, bool isPlatformAdmin = false);
    Task<List<string>> GetDepartmentsAsync(int orgId, int currentUserId = 0, bool isPlatformAdmin = false);
}
