using System.Net;
using Netch.Interfaces;
using Netch.Models;

namespace Netch.Utils;

public class SubscriptionUtil
{
    private readonly NetchAppContext _appContext;
    private readonly INotificationService _notificationService;
    private readonly object _serverLock = new();

    public SubscriptionUtil(NetchAppContext appContext, INotificationService notificationService)
    {
        _appContext = appContext;
        _notificationService = notificationService;
    }

    public Task UpdateServersAsync(string? proxyServer = default)
    {
        return Task.WhenAll(_appContext.Settings.Subscription.Select(item => UpdateServerCoreAsync(item, proxyServer)));
    }

    private async Task UpdateServerCoreAsync(Subscription item, string? proxyServer)
    {
        try
        {
            if (!item.Enable)
                return;

            var request = WebUtil.CreateRequest(item.Link);

            if (!string.IsNullOrEmpty(item.UserAgent))
                request.UserAgent = item.UserAgent;

            if (!string.IsNullOrEmpty(proxyServer))
                request.Proxy = new WebProxy(proxyServer);

            List<Server> servers;

            var (code, result) = await WebUtil.DownloadStringAsync(request);
            if (code == HttpStatusCode.OK)
                servers = ShareLink.ParseText(result);
            else
                throw new Exception($"{item.Remark} Response Status Code: {code}");

            foreach (var server in servers)
                server.Group = item.Remark;

            lock (_serverLock)
            {
                _appContext.Settings.Server.RemoveAll(server => server.Group.Equals(item.Remark));
                _appContext.Settings.Server.AddRange(servers);
            }

            _notificationService.ShowNotification(i18N.TranslateFormat("Update {1} server(s) from {0}", item.Remark, servers.Count));
        }
        catch (Exception e)
        {
            _notificationService.ShowNotification($"{i18N.TranslateFormat("Update servers failed from {0}", item.Remark)}\n{e.Message}", false);
            Log.Warning(e, "Update servers failed");
        }
    }
}
