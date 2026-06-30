# Netch WinUI 3 重构 — 待完成任务

> 当前状态：框架已搭好，Netch.Core + Netch.App 均可编译（0错误0警告）。但还不能实际运行代理。

---

## P0 — 必须完成（才能正常使用）

### 1. 服务器编辑器 UI

**目标：** 用户能添加/编辑各协议的服务器

**位置：** `Netch.App/Views/ServerEditorPage.xaml` + `Netch.App/ViewModels/ServerEditorViewModel.cs`

**实现思路：**
- 每种协议有不同的字段，用 `DataTemplateSelector` 根据 `Server.Type` 切换不同编辑模板
- 或者用一个通用的 `StackPanel` 动态生成控件（类似原项目 ServerForm 的 CreateTextBox 模式）

**需要支持的协议及其字段：**

| 协议 | 特有字段 |
|------|----------|
| Socks5 | Hostname, Port, Username?, Password? |
| Shadowsocks | Hostname, Port, Password, EncryptMethod |
| ShadowsocksR | Hostname, Port, Password, EncryptMethod, Protocol, ProtocolParam, OBFS, OBFSParam |
| Trojan | Hostname, Port, Password, Host?, Path?, TLS options |
| VMess | Hostname, Port, UserID, AlterID, EncryptMethod, TransferProtocol, FakeType, Host, Path, TLS, ServerName |
| VLESS | Hostname, Port, UserID, EncryptMethod, TransferProtocol, Host, Path, TLS, Flow |
| SSH | Hostname, Port, Username, Password/PrivateKey |
| WireGuard | Hostname, Port, LocalAddress, PrivateKey, PeerPublicKey, PreSharedKey?, MTU, DNS |

**具体要做：**
1. 创建 `Netch.App/ViewModels/ServerEditorViewModel.cs`
   - `[ObservableProperty]` 每个通用字段（Remark, Hostname, Port, Group）
   - `[ObservableProperty] string SelectedType` 用于协议切换
   - 协议特有字段用 Dictionary 或各协议子 ViewModel
   - `[RelayCommand] Save` — 创建/更新 Server 对象，写入 `appContext.Settings.Server`，调用 `Configuration.SaveAsync()`

2. 创建 `Netch.App/Views/ServerEditorPage.xaml`
   - 顶部：通用字段（Remark, Type下拉, Hostname, Port）
   - 中间：根据 Type 显示不同面板（用 Visibility 绑定或 ContentControl + DataTemplateSelector）
   - 底部：Save / Cancel 按钮

3. 实现 `IServerEditorService`（在 `Netch.App/Services/ServerEditorService.cs`）
   - `EditServer(Server s)` → 导航到 ServerEditorPage 并传参
   - `CreateServer(string typeName)` → 导航到 ServerEditorPage 空表单
   - 在 App.xaml.cs 启动时设置 `NetchAppContext.ServerEditor = new ServerEditorService(...)`

4. 完善 `ServersPage.xaml`
   - 服务器列表 ListView（显示 Remark + Type + Delay）
   - 工具栏：添加（下拉选协议）、编辑、删除、从剪贴板导入、批量测速
   - 从剪贴板导入：调用 `ShareLink.Parse(clipboard)` 获取服务器列表

**参考原项目：**
- `Netch/Servers/VMess/VMessForm.cs` — 看每个协议需要哪些字段
- `Netch/Servers/V2ray/V2rayConfig.cs` — 看字段枚举值（加密方法、传输协议等）
- `Netch/Utils/ShareLink.cs` — 剪贴板导入逻辑
- `Netch/Utils/ServerHelper.cs` — 服务器类型注册表

---

### 2. 模式编辑器 UI

**目标：** 用户能创建/编辑 Process Mode 和 Route Mode

**位置：** `Netch.App/Views/ModeEditorPage.xaml` + `Netch.App/ViewModels/ModeEditorViewModel.cs`

**Process Mode 编辑器：**
- 规则列表编辑（每行一个进程名/正则）
- 支持 Handle（代理）和 Bypass（绕过）两种规则前缀
- 扫描本机进程列表辅助添加
- 保存为 `.json` 文件到 `mode/` 目录

**Route Mode 编辑器：**
- CIDR 路由规则列表（每行一个 IP/CIDR）
- 支持从文件导入
- 保存为 `.json` 文件到 `mode/` 目录

**完善 ModesPage.xaml：**
- 模式列表 ListView
- 工具栏：创建 Process Mode、创建 Route Mode、编辑、删除、刷新

**参考原项目：**
- `Netch/Forms/ModeForms/ProcessForm.cs` — Process Mode 编辑逻辑
- `Netch/Forms/ModeForms/RouteForm.cs` — Route Mode 编辑逻辑
- `Netch/Models/Modes/ProcessMode/ProcessMode.cs` — 数据模型
- `Netch/Models/Modes/TunMode/TunMode.cs` — Route 数据模型

---

### 3. 驱动文件部署

**目标：** 编译输出目录包含所有必需的二进制文件

**要做：**
在 `Netch.App/Netch.App.csproj` 添加：

```xml
<ItemGroup>
  <!-- 从原项目的 bin 目录复制驱动和工具 -->
  <Content Include="..\Netch\bin\**\*" Link="bin\%(RecursiveDir)%(FileName)%(Extension)">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

**必需文件清单（在输出目录的 `bin/` 子目录）：**
- `Redirector.bin` — 进程流量拦截（C++ 编译产物）
- `RouteHelper.bin` — 路由表操作（C++ 编译产物）
- `tun2socks.bin` — TUN 模式
- `wintun.dll` — WinTUN 驱动
- `nfdriver.sys` — NetFilter 内核驱动
- `nfapi.dll` — NetFilter API
- `pcap2socks.exe` — 共享模式
- `v2ray-sn.exe` + `geoip.dat` + `geosite.dat` — V2Ray 核心
- `aiodns.conf` — DNS 配置
- `stun.txt` — STUN 服务器列表

**注意：** 这些文件在原项目的 `Netch/bin/` 目录下。如果该目录不存在或为空，需要从 Release 包或单独下载。

---

### 4. 实际启动测试

**步骤：**
1. 确保 `bin/` 目录文件齐全
2. 以管理员权限运行 `Netch.App.exe`
3. 添加一个服务器（手动或从剪贴板导入）
4. 选择模式
5. 点击 Start
6. 验证代理是否工作（curl --proxy socks5://127.0.0.1:2801 https://google.com）

**可能遇到的问题：**
- `MainController` 现在是实例类，需确认 DI 注入正确
- `ModeService.Load()` 需要在启动时调用（加载 mode/ 目录的模式文件）
- `ServerHelper` 的反射发现机制需确认能从 Netch.Core 程序集扫描到各 `IServerUtil`
- NF 驱动安装需要管理员权限 + 正确的 nfdriver.sys 路径

---

## P1 — 体验完整

### 5. 系统托盘

**位置：** `Netch.App/Views/MainWindow.xaml.cs` 或独立 `TrayService`

**要做：**
- 在 MainWindow 中添加 `TaskbarIcon`（H.NotifyIcon.WinUI）
- 关闭窗口时如果 `Settings.ExitWhenClosed == false`，则隐藏窗口而非退出
- 托盘右键菜单：显示窗口 / 退出
- 双击托盘图标：显示窗口
- 图标文件：`Netch.Core/Resources/NetchTrayIcon.png`（需复制到 Netch.App/Assets/）

**示例代码（MainWindow.xaml）：**
```xml
xmlns:tb="using:H.NotifyIcon"

<tb:TaskbarIcon x:Name="TrayIcon"
    ToolTipText="Netch"
    IconSource="Assets/NetchTrayIcon.ico">
    <tb:TaskbarIcon.ContextFlyout>
        <MenuFlyout>
            <MenuFlyoutItem Text="Show" Click="ShowWindow_Click" />
            <MenuFlyoutSeparator />
            <MenuFlyoutItem Text="Exit" Click="Exit_Click" />
        </MenuFlyout>
    </tb:TaskbarIcon.ContextFlyout>
</tb:TaskbarIcon>
```

---

### 6. NavigationView 图标修复

**问题：** 当前 MainWindow.xaml 的 NavigationViewItem 没有图标（FontIcon 的 Unicode 转义会崩溃 XAML 编译器）

**解决方案：** 改用 `SymbolIcon`

```xml
<NavigationViewItem Content="Home" Tag="MainPage">
    <NavigationViewItem.Icon>
        <SymbolIcon Symbol="Home" />
    </NavigationViewItem.Icon>
</NavigationViewItem>
```

完整映射：
- Home → `Symbol="Home"`
- Servers → `Symbol="Globe"`
- Modes → `Symbol="Switch"`
- Subscriptions → `Symbol="Sync"`
- Log → `Symbol="Document"`
- Settings → `Symbol="Setting"`
- About → `Symbol="Help"`

---

### 7. 电源事件处理

**位置：** `Netch.App/App.xaml.cs` 或 `MainViewModel`

**要做：**
```csharp
Microsoft.Win32.SystemEvents.PowerModeChanged += (s, e) =>
{
    switch (e.Mode)
    {
        case PowerModes.Suspend:
            // 停止代理
            _mainController.StopAsync().Wait();
            break;
        case PowerModes.Resume:
            // 恢复代理（如果之前在运行）
            break;
    }
};
```

---

### 8. 窗口位置记忆

**位置：** `Netch.App/Views/MainWindow.xaml.cs`

**要做：**
- 窗口关闭/移动时保存位置到 Settings
- 启动时恢复位置
- 使用 `AppWindow` API：`this.AppWindow.Move(new PointInt32(x, y))` 和 `this.AppWindow.Resize(new SizeInt32(w, h))`
- 在 `Setting.cs` 中添加 `WindowX/WindowY/WindowWidth/WindowHeight` 属性

---

### 9. i18N 绑定

**当前状态：** XAML 里硬编码英文字符串

**方案A（简单）：** 在 code-behind 中用 `i18N.Translate()` 手动设置
**方案B（优雅）：** 创建 XAML MarkupExtension：

```csharp
// Netch.App/Helpers/LocalizeExtension.cs
[MarkupExtensionReturnType(ReturnType = typeof(string))]
public class Localize : MarkupExtension
{
    public string Key { get; set; }
    protected override object ProvideValue() => i18N.Translate(Key);
}
```

使用：
```xml
<TextBlock Text="{local:Localize Key=Start}" />
```

---

## P2 — 锦上添花

### 10. 从剪贴板导入服务器

- 读取剪贴板文本：`Windows.ApplicationModel.DataTransfer.Clipboard`
- 调用 `ShareLink.Parse(text)` 解析
- 批量添加到服务器列表
- 在 ServersPage 的工具栏加一个"从剪贴板导入"按钮

### 11. 批量测速

- `DelayTestHelper` 已在 Core
- 在 ServersPage 加"全部测速"按钮
- 逐个调用 `server.PingAsync(useTcpPing)`
- 更新 ListView 中的延迟显示

### 12. 深色/浅色主题

在 App.xaml.cs 中：
```csharp
if (App.MainWindow.Content is FrameworkElement rootElement)
{
    rootElement.RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
}
```
在 Settings 中加一个 `Theme` 属性（System/Light/Dark）。

### 13. 自动更新

- `UpdateChecker` 是静态类，已在 Core
- 启动时检查 GitHub Release
- 如果有新版本，在 MainPage 显示 InfoBar 提示

---

## 已知技术问题备忘

1. **XAML 编译器崩溃** — `FontIcon Glyph="&#xE80F;"` 这种 Unicode 转义在当前环境会导致 XamlCompiler.exe (net472) 崩溃。用 `SymbolIcon` 替代，或者在 code-behind 设置图标。

2. **MainController 是实例类了** — 原来是 static，现在通过 DI 注入。确保所有调用方都从 DI 获取而非 `MainController.StartAsync()` 静态调用。

3. **ServerHelper 反射扫描** — `ServerHelper.GetUtilByTypeName()` 通过反射发现 `IServerUtil` 实现。需确认它扫描的是 `Netch.Core` 程序集（检查 `ServerHelper.cs` 中的 Assembly 引用）。

4. **Configuration.LoadAsync()** — 在 App.xaml.cs 中 `.Wait()` 调用，如果路径不对会静默失败。确保 `data/settings.json` 路径相对于 exe 位置正确。

5. **ModeService.Load()** — 需要在 App 启动后调用一次来加载 `mode/` 目录下的模式文件到 `appContext.Modes` 列表。当前 App.xaml.cs 中缺少这一步。

6. **App.xaml 不能有 XamlControlsResources** — `<XamlControlsResources />` 会导致编译器崩溃（同样是引用数量过多的问题）。运行时应该还能工作（WinUI 默认加载），但主题可能不完整。如果运行时样式不对，尝试在 OnLaunched 中手动加载：
   ```csharp
   Resources.MergedDictionaries.Add(new XamlControlsResources());
   ```

---

## 文件结构参考

```
Netch.App/
├── App.xaml / App.xaml.cs          ← DI + 启动
├── Program.cs                      ← 入口（单实例）
├── app.manifest                    ← 管理员权限
├── Netch.App.csproj
├── Assets/                         ← 图标等静态资源
├── Converters/
│   └── DelayToColorConverter.cs
├── Helpers/
│   └── LocalizeExtension.cs       ← [TODO] i18N XAML绑定
├── Services/
│   ├── NavigationService.cs
│   ├── StatusReporterService.cs
│   ├── NotificationServiceImpl.cs
│   ├── ModeListManagerService.cs
│   ├── WindowActivatorService.cs
│   └── ServerEditorService.cs     ← [TODO]
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── SettingsViewModel.cs
│   ├── SubscriptionViewModel.cs
│   ├── ServerEditorViewModel.cs   ← [TODO]
│   ├── ModeEditorViewModel.cs     ← [TODO]
│   └── LogViewModel.cs
└── Views/
    ├── MainWindow.xaml / .cs
    ├── MainPage.xaml / .cs
    ├── SettingsPage.xaml / .cs
    ├── ServersPage.xaml / .cs      ← [TODO] 完善
    ├── ServerEditorPage.xaml / .cs ← [TODO]
    ├── ModesPage.xaml / .cs        ← [TODO] 完善
    ├── ModeEditorPage.xaml / .cs   ← [TODO]
    ├── SubscriptionPage.xaml / .cs
    ├── LogPage.xaml / .cs
    └── AboutPage.xaml / .cs
```
