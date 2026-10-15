using OmniProduct_CoreDomain.Events;
using OmniProduct_CoreDomain.Models;

namespace OmniProduct_CoreDomain.Services;

public class NotificationService
{
    private readonly List<Notification> _notifications = new();

    public void AddNotification(Notification notification)
    {
        _notifications.Add(notification);
    }

    public List<Notification> GetNotifications()
    {
        return _notifications.ToList();
    }
}
