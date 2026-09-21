using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface IAccessRequestService
{
    Task<List<AccessRequestDto>> GetIncomingRequestsAsync(int userId, string userRole);
    Task<List<AccessRequestDto>> GetMyRequestsAsync(int userId);
    Task<List<AccessRequestDto>> GetApprovedRequestsAsync(int userId, string userRole);
    Task<AccessRequestDto> CreateRequestAsync(int userId, CreateAccessRequestDto request);
    Task<AccessRequestDto?> DecideRequestAsync(int deciderUserId, string deciderUsername, int requestId, DecideAccessRequestDto decision);
    Task<List<OrgWithManagersDto>> GetOrganizationsWithManagersAsync();
}
