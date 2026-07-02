using System.Runtime.InteropServices;
using Microsoft.Win32;
using Netch.App.Models;

namespace Netch.App.Services;

public static class AppDiscoveryService
{
    private static readonly string[] SkipKeywords =
    [
        "android studio",
        "antigravity",
        "blend for visual studio",
        "bcompare",
        "cc switch",
        "git gui",
        "github desktop",
        "gpuview",
        "hermes",
        "idle (python",
        "microsoft edge",
        "google chrome"
    ];

    public static List<InstalledApp> DiscoverInstalledApps()
    {
        var apps = new Dictionary<string, InstalledApp>(StringComparer.OrdinalIgnoreCase);

        ScanRegistry(apps, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanRegistry(apps, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall");
        ScanStartMenu(apps);

        return apps.Values
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ScanRegistry(Dictionary<string, InstalledApp> apps, string keyPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(keyPath);
        if (key == null) return;

        foreach (var subKeyName in key.GetSubKeyNames())
        {
            try
            {
                using var subKey = key.OpenSubKey(subKeyName);
                if (subKey == null) continue;

                var displayName = subKey.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName)) continue;
                if (ShouldSkip(displayName)) continue;

                var systemComponent = subKey.GetValue("SystemComponent");
                if (systemComponent is int sc && sc == 1) continue;

                var installLocation = subKey.GetValue("InstallLocation") as string;
                var displayIcon = subKey.GetValue("DisplayIcon") as string;

                var installPath = ResolveInstallPath(installLocation, displayIcon);
                if (installPath == null) continue;

                if (!apps.ContainsKey(installPath))
                {
                    apps[installPath] = new InstalledApp
                    {
                        Name = displayName,
                        InstallPath = installPath,
                        ExePath = ResolveExePath(displayIcon)
                    };
                }
            }
            catch
            {
            }
        }
    }

    private static void ScanStartMenu(Dictionary<string, InstalledApp> apps)
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu)
        };

        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;

            foreach (var lnk in EnumerateFilesSafe(folder, "*.lnk"))
            {
                try
                {
                    var targetPath = ResolveShortcut(lnk);
                    if (targetPath == null) continue;
                    if (!targetPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(targetPath)) continue;

                    var installPath = Path.GetDirectoryName(targetPath);
                    if (installPath == null) continue;
                    if (IsSystemPath(installPath)) continue;

                    var name = Path.GetFileNameWithoutExtension(lnk);
                    if (ShouldSkip(name)) continue;

                    if (!apps.ContainsKey(installPath))
                    {
                        apps[installPath] = new InstalledApp
                        {
                            Name = name,
                            InstallPath = installPath,
                            ExePath = targetPath
                        };
                    }
                }
                catch
                {
                }
            }
        }
    }

    private static bool ShouldSkip(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        return SkipKeywords.Any(normalized.Contains);
    }

    private static string? ResolveInstallPath(string? installLocation, string? displayIcon)
    {
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
            return installLocation.TrimEnd('\\');

        if (!string.IsNullOrWhiteSpace(displayIcon))
        {
            var iconPath = displayIcon.Split(',')[0].Trim('"');
            if (File.Exists(iconPath))
            {
                var dir = Path.GetDirectoryName(iconPath);
                if (dir != null && !IsSystemPath(dir))
                    return dir;
            }
        }

        return null;
    }

    private static string? ResolveExePath(string? displayIcon)
    {
        if (string.IsNullOrWhiteSpace(displayIcon)) return null;

        var path = displayIcon.Split(',')[0].Trim('"');
        return File.Exists(path) && path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? path : null;
    }

    private static bool IsSystemPath(string path)
    {
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        return path.StartsWith(winDir, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, string pattern)
    {
        Queue<string> dirs = new();
        dirs.Enqueue(root);

        while (dirs.Count > 0)
        {
            var dir = dirs.Dequeue();
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(dir, pattern);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }

            foreach (var f in files)
                yield return f;

            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(dir))
                    dirs.Enqueue(subDir);
            }
            catch (UnauthorizedAccessException) { }
            catch (DirectoryNotFoundException) { }
        }
    }

    private static string? ResolveShortcut(string lnkPath)
    {
        try
        {
            var shellLinkType = Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"));
            if (shellLinkType == null) return null;

            var shellLink = Activator.CreateInstance(shellLinkType);
            if (shellLink == null) return null;

            var persistFile = (IPersistFile)shellLink;
            persistFile.Load(lnkPath, 0);

            var link = (IShellLink)shellLink;
            var sb = new char[260];
            unsafe
            {
                fixed (char* p = sb)
                {
                    link.GetPath(p, 260, IntPtr.Zero, 0);
                }
            }

            var target = new string(sb).TrimEnd('\0');
            return string.IsNullOrWhiteSpace(target) ? null : target;
        }
        catch
        {
            return null;
        }
    }

    [ComImport]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName);
        void GetCurFile(out IntPtr ppszFileName);
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IShellLink
    {
        void GetPath(char* pszFile, int cch, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription(char* pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory(char* pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments(char* pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out ushort pwHotkey);
        void SetHotkey(ushort wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation(char* pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }
}