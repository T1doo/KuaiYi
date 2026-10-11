# ADR 0002：探针的最小依赖、字节生命周期与证据

当前探针只调用 WPF/UIA/Win32，不使用 WinRT。因此采用 `net10.0-windows` 和 `win-x64` 启动器，而不引入带版本的 Windows TFM 自动下载的 WinRT 投影。

初次交叉还原曾下载 Microsoft.Windows.SDK.NET.Ref 10.0.19041.57。许可证元数据核查发现 `requireLicenseAcceptance=true` 和 Windows SDK 许可链接。没有执行显式接受协议操作；该包对本切片没有用途，已从有效项目依赖及探针打包输出去除。后续 OCR/截图 WinRT 引用与原生 Windows SDK 仍需独立核实许可/授权，不将此下载缓存视作已获授权安装原生 SDK。

有效 WPF 引用和 x64 apphost 均由锁定 SDK 10.0.401 下载精确 10.0.12 版本；NuGet 下载依赖不全部进入 packages.lock，因此另记实际 framework downloadDependencies 及摘要，普通构建同时固定 SDK 与 locked-mode。

采用 PMv2 manifest。官方说明 GetDpiForMonitor 不应由 per-monitor-aware 线程调用；探针在目标监视器内创建不可见、非激活 STATIC HWND，使用 GetDpiForWindow 后销毁，既不移动主锚点窗口，也不请求焦点。此方式已交叉编译，真实跨屏 DPI 结果仍 NotRun。[官方 GetDpiForMonitor 约束](https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor)

Native Messaging 返回的 JSON 文档必须拥有其解析数据，不能引用随后清零的接收 buffer。ParseValue 从 Utf8JsonReader 复制所解析字节，并检查无尾随值和严格 UTF-8，再清除临时 buffer。独立进程双帧测试验证返回值正确。[官方 ParseValue 数据所有权](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.jsondocument.parsevalue?view=net-10.0)

GrowthHarness 按实际 HWND 记录边界，同时记录期望值；最多保留 100000 个样本，退出时落盘。同步帧中不执行磁盘 IO。布局调用采样不能代表屏幕呈现时间，摘要不自动判定桌面验收通过。
