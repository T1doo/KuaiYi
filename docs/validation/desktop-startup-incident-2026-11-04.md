# 桌面测试启动错误与输入语言通知

## 已查明的事实

1. 本聊天工具调用记录在 2026-11-04 18:44:42（北京时间）发送过 `Control_L+space`，18:44:57 发送过 `Alt_L+Shift_L`，用于中文输入法验证。截图里的 Windows 输入语言切换通知与后者对应；不能再将是否执行过切换描述为未知。查到一次该组合键调用，不能由此断言通知反复出现的全部机制已确认。
2. `aurora-cloud-review-b` 原进程堆栈显示 `NativeTray.Restore` 中 NIM_ADD 失败。启动命令处于默认隔离环境。
3. 本次只读对照：隔离进程 Restricted=True、桌面为 CodexSandboxDesktop、FindWindow(Shell_TrayWnd)=false；正常进程 Restricted=False、桌面 Default、任务栏窗口=true。两者同属当前登录会话。错误与在没有任务栏的隔离桌面中注册托盘图标相符；未在用户桌面重现故障。
4. Microsoft 的 Shell_NotifyIcon 文档只约定 BOOL 返回值，没有承诺 GetLastError。旧实现失败后读取 LastError，产生“未指定的错误”，不足以推断系统原因。

## 修复

- 桌面探针在创建 WPF、配置、窗口或托盘前检查任务栏。缺失时输出明确原因、返回 5；不重试。纯管道／进程探针不套用此条件。
- 新增 `--check-desktop` 只读诊断分支，不创建 Application／窗口／托盘。
- 原生托盘构造也先检查任务栏；API 失败标明 NIM_ADD／NIM_SETVERSION／NIM_MODIFY／NIM_DELETE，不读取未承诺的错误码。保留原先“无托盘不得留下不可访问应用”的约束。
- 测试程序最外层捕获异常、记录堆栈并返回 3，不让异常直接逃逸，也不自动重启。此前已补 app.Run 异常和零帧失败记录，旧错误 summary 保留且不视为通过。
- 不再自动发送输入语言切换组合键。恢复桌面输入测试须等用户明确表示方便；不修改 Windows 的输入法、快捷键或通知设置来掩盖问题。

## 验证范围

- SDK 10.0.402 Release 构建 0 警告、0 错误，日志 `artifacts/tray-startup-fix-build.log`。
- 同一新增预检命令：隔离环境退出码 5、输出 DesktopUnavailable；正常环境退出码 0、输出 DesktopAvailable。均只读，不创建窗口。
- 原错误未通过重新启动 UI 复现；最新 UI 启动及托盘实际添加仍未验收。用户截图不是系统崩溃证据，亦不能保证其他无关程序没有故障。
- 先前 292 项宽筛选测试中的 32 项失败另有缺失 CaptureBroker 产物与文件权限／替换问题；不将其冒充本次已解决或产品前端失败。

参考：[Shell_NotifyIcon 官方说明](https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shell_notifyicona)。
