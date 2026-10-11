# P1 启动骨架复核

基线为监督方限定签收的 `4f4cde1b27cfa9368d7fb8fcc28d0162eef1b2f0`。本轮补实际启动接线，P0/E06仍Blocked、P1仍InProgress，没有跳过Windows退出门槛。

正常入口从旧P0评审面板改为一个实体圆形非激活置顶浮钮和原生托盘；旧面板通过 `--platform-review` 保留，其本地OCR/OLE许可没有变成模型许可。ShellLifecycle启动通过生产SettingsCoordinator读取已有配置元数据、绑定和模式，始终Off，不解密旧Key、不读取选区或联网。App使用保存结果创建正常SettingsWindow，沿用原SettingsRecoveryDraft、原子保存和定向撤销通知。设置草稿/实际保存结束后才退出；锁屏和退出立即掩码秘密，拒绝迟到启用。

主按钮和托盘复用同一RuntimeGate。首次启用显示origin/模式，本次会话明确许可；缺项打开设置且保持红色。EnableIntent在Gate最终锁内验证设置版本、epoch、当前不透明profile绑定、暂停和disposed状态，旧弹窗不能跨隐藏/锁屏/保存恢复On。正式Shell的严格门控在请求准入及每次发送处都要求当前绑定；无绑定API启动拒绝，本地身份后来提升On也不能发送。完整P4设置转协议/模型调度尚未交付，新增代码不伪造翻译输出。

单实例实际使用OS命名Mutex；Windows以当前token SID+Process.SessionId摘要命名并使用Local命名空间，正常退出释放，崩溃由OS释放。二次启动仅发送同scope的Show窗口消息，不转发任何文本/图像/路径。这个消息不是授权或内容IPC。同用户同会话只能一个正常应用；当前用户/会话真实Windows行为和对象DACL未运行。[官方命名空间说明](https://learn.microsoft.com/en-us/windows/win32/termserv/kernel-object-namespaces)。

托盘使用Shell_NotifyIconW版本4、文本启停状态、菜单同源命令及TaskbarCreated重建；图标暂用Windows共享图标，正式视觉资产留P2。Windows回调/键盘菜单/Explorer重启尚未运行。[官方托盘文档](https://learn.microsoft.com/en-us/windows/win32/shell/notification-area)。

位置接线枚举当前工作区，用MONITORINFOEX设备名代替不稳定的HMONITOR指针存盘；DPI通过目标位置非显示HWND的GetDpiForWindow取得。OverlayPositionMapper已存在，直接复用归一化捕获/恢复，缺显示器回主屏、按12 DIP安全边界夹紧。拖动按系统与6 DIP严格阈值，保存走原配置事务；启动加载不能覆盖本次已移动按钮，隐藏布局不带SWP_SHOWWINDOW。设备名随显示拓扑可能重新分配，不声称物理屏永久身份；现阶段安全夹紧优先。[官方MONITORINFOEX](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfoexw)、[官方DPI查询](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow)。

本轮真实便携验证覆盖生产配置读写、恢复、绑定、取消链、位置映射和OS子进程互斥；公开合成profile无真实密钥/请求。Windows渲染、鼠标、托盘、会话SID/DACL、DPAPI、多屏实际HWND与退出句柄均NotRun。待执行步骤见[p1-shell-windows-checklist.md](p1-shell-windows-checklist.md)。精确构建/测试、包大小/hash及逐文件冻结见[test-evidence/2026-10-11-p1-shell-summary.json](test-evidence/2026-10-11-p1-shell-summary.json)。

环境断连提示后再次实际工具成功：HEAD仍4f4cde1、本轮未提交；既有执行会话App构建零警告/错误及20新例通过返回。本地缺origin开发分支追踪引用是fetch refspec只取main，`git ls-remote`实查远端开发分支4f4cde1、main51d0774；没有删除未知锁、修改refspec、另建写入者或换环境。后续提交/推送结果单独记录，不用本地引用缺失猜测远端状态。

上一报告687/686算式改正为687/687；旧签收源码、raw证据和原报告字节仍可从4f4cde1读取，八份已签收审查ZIP逐个保留和复核。

实际结果：新增24/24；全回归Core634+Provider77=711/711，Node63/63；25工程locked Release零警告/错误。三个开发编译问题均修复且日志保留；最后设置退出避免重复Close后重编，无测试/断言删改。单实例跨用户/会话只使用合成scope参数，未跨真实操作系统账户执行。审查ZIP796238字节，SHA256 `96f031f13c1cb549b1da660497e935ab2ad7ad627d785cbe578659629eb183ba`，43文件、5个PE32+ x64主机，manifest逐项通过。

同一运行会话已确认的origin许可在Off后保留，换域名或重启不复用；新添1例，最终24项新增测试。之前23例/710全回归与初包metadata保留为pre-consent证据，最终711回归和新包单独冻结。
