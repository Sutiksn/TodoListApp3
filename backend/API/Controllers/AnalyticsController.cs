using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoListApp2.Services;

namespace TodoListApp2.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("personal")]
    public async Task<IActionResult> GetPersonalAnalytics()
    {
        int userId = GetUserId();
        var data = await _analyticsService.GetPersonalAnalyticsAsync(userId);
        return Ok(data);
    }

    [HttpGet("team")]
    public async Task<IActionResult> GetTeamAnalytics([FromQuery] int workspaceId)
    {
        var data = await _analyticsService.GetTeamAnalyticsAsync(workspaceId);
        return Ok(data);
    }

    [HttpGet("project")]
    public async Task<IActionResult> GetProjectAnalytics([FromQuery] int workspaceId)
    {
        var data = await _analyticsService.GetProjectAnalyticsAsync(workspaceId);
        return Ok(data);
    }

    [HttpGet("business")]
    public async Task<IActionResult> GetBusinessAnalytics([FromQuery] int orgId)
    {
        int userId = GetUserId();
        bool isPlatformAdmin = User.IsInRole("PlatformAdmin");
        var data = await _analyticsService.GetBusinessAnalyticsAsync(orgId, userId, isPlatformAdmin);
        return Ok(data);
    }

    [HttpGet("admin")]
    public async Task<IActionResult> GetAdminPlatformAnalytics()
    {
        var data = await _analyticsService.GetAdminPlatformAnalyticsAsync();
        return Ok(data);
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out int id) ? id : 0;
    }
}
