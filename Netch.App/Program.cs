using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;

namespace Netch.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var isRedirect = DecideRedirection();
        if (!isRedirect)
        {
            Microsoft.UI.Xaml.Application.Start(p =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
    }

    private static bool DecideRedirection()
    {
        var mainInstance = AppInstance.FindOrRegisterForKey("NetchMainInstance");
        if (!mainInstance.IsCurrent)
        {
            var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            mainInstance.RedirectActivationToAsync(activatedArgs).AsTask().Wait();
            return true;
        }

        return false;
    }
}
