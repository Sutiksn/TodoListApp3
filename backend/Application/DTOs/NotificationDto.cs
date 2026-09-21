namespace TodoListApp2.Models.DTOs;

public record NotificationDto(
    int Id,
    string Title,
    string Message,
    string? LinkUrl,
    string Type,
    bool IsRead,
    DateTime CreatedAt
);
