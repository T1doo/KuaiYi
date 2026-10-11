# P2 生产浮窗与跨DPI拖动复核

基线为监督限定签收 `359cf26cbf3d87996bd4c56bd82cd1f9d5eaac85`。先实查工具/HEAD/干净分支后开工；再次断连通知后安全只读命令仍成功，HEAD未变、未提交文件及构建日志保留，不删除锁、换环境或另建写入者。

原缺口是WM_DPICHANGED走通用位置恢复，CancelPointer清除同一次drag，MouseUp不再产生Released/保存。本轮保留捕获手势和Moved闩锁，在原始按下抓取比例基础上按新DPI重基物理坐标；整体卡片先夹紧再Rebase。WPF负责DPI transform/建议尺寸，适配器在其更新后重施已经验证的bounds；显示器实际消失或取消/锁屏则结束手势，不误触On。仅删CancelPointer并不正确，本轮同时废弃旧动画坐标、冻结可见卡片、松手重规划并通过原SettingsCoordinator保存。真实消息顺序仍待桌面验证。[WM_DPICHANGED官方合同](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged)、[WPF DPI回调](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.ondpichanged?view=windowsdesktop-10.0)。

生产ShellWindow现在实际组合OverlayFrames/OverlayResults与WPF Canvas、正文TextBox、工具条、进度环和同一HWND。OverlayFrames直接给出每帧四边、220ms首次ease-out、180ms收起和80ms内容淡出；反向或移动恢复从已显示几何/透明度继续，不每token建Storyboard。文本按最终宽度测量/裁切，每50ms合并；高度单向，达到工作区上限内部滚动。Rendering帧回调收敛即停止；无几何变化的等待环不反复重建native region，主题注册表读取只在设置/系统事件点。圆角按外扩连续24→16DIP。每帧验证A⊆C⊆W，坐标转换仍物理像素与DIP分离。[官方Rendering事件](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.compositiontarget.rendering?view=windowsdesktop-10.0)。

OverlayResults只有会话内有界文字/最多三版本；新来源必须有效身份才能替换，重复Begin不清内容，旧部分版本标停止。实际IdentityEventReceiver在UI目的队列最终锁内检查；Off/锁屏/替换后的迟到文字被拒绝。手动收起、Off缓存回看、隐藏拒回看、当前请求停止和锁屏/退出清内存已接到同一Gate。回看不读取来源、不启用、不启动请求。当前纯文本显示，没有伪装Markdown、重生成调度或P4接口完成。

移动冻结的是整张已经显示卡片及正文宽度，不清缓存或取消原模型任务；跨DPI/小屏允许安全缩小。正在拖动时首批正文只缓存，松手才展开；窗口被工作区夹紧离开鼠标后仍保留原始抓取比例。工具条350/250ms与整窗悬停/键盘焦点关联；滚动遵守24DIP跟随条件。非激活、主题/高对比度、减少动画和主动键盘/正文阅读有真实WPF接线，Windows效果均未运行。

独立GrowthHarness的 `--actual-overlay` 使用生产ShellWindow并通过同一目的队列发送公开fixture；可调5/30/100字每秒或2000字突发。CSV读回实际HWND、实际WPF按钮屏幕矩形、工作区和前台变化。生产App不引用工具、没有fixture数据，友元仅供此独立工具调用内部观测入口。项目锁文件只增加App/NativeHost工程引用，没有新增第三方包、安装或协议接受。

新增本地CI入口调用现有locked Managed/Offline脚本，并从原TRX/JUnit校验全部实际执行、无失败/跳过；不存在远端GitHub workflow，本轮未启动远端CI。不会把Linux自动化、CSV工具的NotEvaluated或交叉编译改成WindowsPass。P0/E06仍Blocked、P1/P2仍InProgress，106整体验收仍NotRun。独立包和精确原始证据见本轮summary；待执行步骤见[p2-overlay-windows-checklist.md](p2-overlay-windows-checklist.md)。

实际结果：34新增项，Core668+Provider77=745/745，Node63/63；25托管工程locked Release零警告/错误。初24、扩31的原始TRX保留；新增2例随后实际CI全回归。补空delta不算首正文1例后重新完整CI和locked审查构建。原33例/744回归及首包摘要保留为pre-empty-delta证据。公开探针流速改用真实单调时钟，并跳过隐藏窗口的几何观测。包1445739字节，SHA256 `0f59b4deb79b48fd34bb9fa790e3c48eb87eb9500768da8c1bbef0cdf4df6832`，68文件、生产app39个文件和独立公开probe25个文件，全部exe PE32+ x64、逐项manifest通过；九旧包逐一hash未变。
