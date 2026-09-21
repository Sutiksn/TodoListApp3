using Microsoft.EntityFrameworkCore;
using TodoListApp2.Data;
using TodoListApp2.Models.DTOs;
using TodoListApp2.Models.Entities;

namespace TodoListApp2.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateNotificationAsync(int userId, string title, string message, string? linkUrl = null, string type = "System")
    {
        try
        {
            var notif = new Notification
            {
                UserId = userId,
                Title = title.Trim(),
                Message = message.Trim(),
                LinkUrl = linkUrl,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notif);
            await _context.SaveChangesAsync();
        }
        catch
        {
            // Fail-safe for async notifications
        }
    }

    public async Task<List<NotificationDto>> GetUserNotificationsAsync(int userId, bool unreadOnly = false)
    {
        var query = _context.Notifications.Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(30)
            .Select(n => new NotificationDto(n.Id, n.Title, n.Message, n.LinkUrl, n.Type, n.IsRead, n.CreatedAt))
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(int userId, int notificationId)
    {
        var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (notif == null) return false;

        notif.IsRead = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkAllAsReadAsync(int userId)
    {
        var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        foreach (var n in unread)
        {
            n.IsRead = true;
        }
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ClearNotificationsAsync(int userId)
    {
        var notifs = await _context.Notifications.Where(n => n.UserId == userId).ToListAsync();
        _context.Notifications.RemoveRange(notifs);
        await _context.SaveChangesAsync();
        return true;
    }
}
