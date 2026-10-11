# ADR 0001：平台分层与证据边界

状态：Accepted for P0 implementation；Windows 宿主可行性尚未实测。

保留 C#/.NET 10/WPF + Win32/COM；核心逻辑 net10.0，Windows 实现独立 Windows TFM。现有云环境仅执行纯逻辑、真实本机 HTTP/协议与可行托管交叉构建。

UIA 使用无窗口 MTA Broker，由父进程硬截止终止卡住的 COM 调用；不将线程取消当作 COM 调用必然取消。

OLE 必须协商可选 IDataObjectAsyncCapability。未协商时 Drop 返回前取得受限副本；STA 消息泵、同步源、COM 封送、COPY-only 与故障隔离均由真实探针决定，不能先假定 WPF 包装达标。

非激活 HWND 与主动键盘/Narrator 阅读必须同时验证；WS_EX_TRANSPARENT 不用作通用点击穿透。几何每帧在物理像素中收敛，并保持按钮锚点。

安装态逐个检查主 EXE / ImageWorker 身份，再执行 OCR/现代菜单；注册成功不能等同运行成功，证书信任单独授权。

参考（只读核实于 2026-10-11）：

- https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading
- https://learn.microsoft.com/en-us/windows/win32/api/shldisp/nn-shldisp-idataobjectasynccapability
- https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios
- https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles
- https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps
- https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/winrt-api-desktop-app-support
- https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100

外部阻塞只影响相应验收；可独立开发部分继续推进，P0 整体退出门槛不降。
