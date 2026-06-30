using Netch.Interfaces;

namespace Netch.App.Services;

public class NotificationServiceImpl : INotificationService
{
    public void ShowNotification(string text, bool isInfo = true)
    {
        // TODO: Show WinUI InfoBar or tray notification
        Serilog.Log.Information("[Notification] {Text}", text);
    }
}
