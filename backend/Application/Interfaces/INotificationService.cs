using TodoListApp2.Models.DTOs;

namespace TodoListApp2.Services;

public interface INotificationService
{
    Task CreateNotificationAsync(int userId, string title, string message, string? linkUrl = null, string type = "System");
    Task<List<NotificationDto>> GetUserNotificationsAsync(int userId, bool unreadOnly = false);
    Task<int> GetUnreadCountAsync(int userId);
    Task<bool> MarkAsReadAsync(int userId, int notificationId);
    Task<bool> MarkAllAsReadAsync(int userId);
    Task<bool> ClearNotificationsAsync(int userId);
}
