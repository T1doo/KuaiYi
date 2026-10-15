# P2-04：阅读操作使过期滚动回调失效

基线f741e047ced54d95c0f3745d79a4532084da9366/tree1765de4b4cbea4363b1ad2d162e8ec17f4482a93，单worktree、独占写入锁。新本地分支dev/overlay-reading-scroll-local不匹配任何现有workflow；本批不push、不Windows CI、不CMake生命周期、不成员探针、不实际模型调用。

## 先结算现有诊断证据的边界

只核现有源码格式及两次明确保留的baseline/Embedded结果目录，没有跟随日志中路径访问任何位置。两目录未保全CMakeVersion.local.log、NativeConfigure.local.log、native/CMakeCache.txt、native/CMakeFiles/CMakeConfigureLog.yaml的原字节；当时四文件Projected不等于现在拥有原文。完整job日志也没有实际MSVC identification版本行、/Z7 token或ABI源命令标记。合成fixture文件不能替代这两次原文件。

| 所问事实 | 源格式能否设计有限白名单 | 本批现有实际证据 |
| --- | --- | --- |
| MSVC精确编译器版本 | NativeConfigure原输出有固定MSVC identification版本行，可限定有界数字版本；现project_bytes只保留CompilerIdSeen。编译器版本也不能等同完整VS/toolset安装版本。 | 原行未保留；Unknown，不能从true恢复版本。 |
| 实际debug-info格式 | Cache的CMAKE_MSVC_DEBUG_INFORMATION_FORMAT即使存在也只能证明配置值。实际ABI编译flag需在严格关联的构建输出中另识别有限枚举；当前投影只保留生成器。 | 只有入口请求Embedded及有限回执，不证明实际/Z7；Unknown。 |
| ABI命令/参数 | 固定CMake3.31.6的cmCoreTryCompile.cxx1503–1511写cmakeVariables及buildResult.stdout到YAML；现投影仅确认try_compile-v1。原命令含路径等不能直接导出，变量值也不等于最终编译/链接命令。 | 无YAML原字节/命令；Unknown，不重建或补造。 |

保持原投影与读取器不改，不为此加CI或新采集外围。具体原目录文件可用性/有限日志标记计数与hash见[本批证据](../test-evidence/2026-10-15-overlay-reading-scroll.json)。两次root Exit0与ConfigureDone/GenerateDone true仍成立；Job等待均Timeout、脚本Exit3、救援后清理和collector清理确认。这是三个分别记录的事实，不提升为生命周期成功或成员身份结论。

## 对照SPEC P0—P8与当前代码后的选择

重读SPEC16.1—16.10与最近P0—P4文字Alpha计划，再核当前App/Core/Providers/Imaging实际入口；以下为可推进性判断，不更改阶段退出门槛。

| 阶段 | 当前代码/最近计划对应情况 | 本批独立工作判断 |
| --- | --- | --- |
| P0 | 真实UIA/OLE/布局/OCR/身份探针及进程结算实现存在；原configure/四生命周期与E06真实门未过。 | Windows平台证明仍Blocked；停止重复配置实验。 |
| P1 | RuntimeGate、ShellLifecycle、Settings/SecretStore及真实设置接线已存在。 | 不重复启停/秘密事务；实际WPF/DPAPI验收仍需Windows。 |
| P2 | OverlayFrames、OverlayPresentation和OverlayResults已接生产文字流；FlushResult的Loaded回调缺少用户阅读操作失效标记。 | **选择此项：修复SPEC4.6阅读锚点与P2-04现有用户路径，既不依赖CMake也不新增采集/网络。** |
| P3 | SelectionFlow、复制兼容、TextDropFlow、HotkeyFlow及来源/身份门已接；最新计划保留真实跨应用验收。 | 不重复捕获链或放宽250/600ms/清理边界。 |
| P4 | 双协议/SSE、阶段时限、会话版本、模板与文字浮窗生产接线已存在。 | 文字Alpha的可读性正受P2缺口影响；先修当前路径，不重复适配器/调度。 |
| P5 | 既有正式text Drop与模式门切片存在，完整E组/同步COM与来源交互仍未验收。 | 最近计划未进入完整P5；不能把新隔离诊断当产品退出证明。 |
| P6 | 当前截图入口仍明确本地预览，ImageUiWorkflow完成本地OCR；两协议已有image body能力，完整分类/最终视觉生成尚无生产闭环。 | 是真实后续缺口，但跨越当前未退出P0—P4及P5，范围/所有权/视觉验收均不同；本批不重复底层图片适配器或宣称图像Beta。 |
| P7 | ShellExtension、NativeHost/扩展IPC及打包脚本已有源码，安装身份/菜单与浏览器实测仍待。 | 实际安装/注册/升级证据依赖Windows，不借此新增权限或发布。 |
| P8 | 离线状态/协议/边界测试已大量存在；完整A—L、可访问性/浸泡/质量及发布验收仍未过。 | 对当前产品修复做相关回归；不把本批端口测试替代实机与完整验收。 |

这是在已核当前生产代码和已授权P0—P4范围内选择的高价值修复：已有长译文阅读会受延迟动作干扰，有具体缺口和可重复反例，优先于新建诊断外围或跨入P5/P6新链。不是宣称找尽所有产品缺陷。

## 实质修改及实际测试

将原Loaded滚动回调抽至生产partial OverlayScrollFlow.cs。每次排队捕获滚动序号及原resultRevision；仅最新序号、原结果、未退出/未锁屏时才发出ScrollTo操作。已经观察到的wheel、键盘、鼠标/触摸阅读操作、捕获中的滚动条变化、“有新内容”、隐藏和收起先使旧序号失效。输入用AddHandler(...,handledEventsToo:true)只观察，不置Handled，不改原焦点行为。

本批没有新服务、轮询、窗口、网络、内容缓存或持久阅读数据；只加一个UI线程long和原回调守卫。原数据门/HTTP、50ms刷新与24DIP跟随阈值、捕获预算/隔离/救援/清理、Job成员查询以及失败实验参数均保持原样。P2-04仍InProgress，P0/E06、106整体和生产验收状态保持。

测试直接Link同一个生产partial，使用明确的Body/Dispatcher端口和真实RuntimeGate锁屏API，不是在Linux运行WPF。最终相同11条断言：owned临时checkout替换为等价原回调后6失败/5通过；修复后Provider全套532/532通过，其中11条滚动专项、原有521条全部执行，0跳过。App使用SDK10.0.401、RestoreLockedMode、EnableWindowsTargeting的Release交叉构建0警告/0错误；无依赖/锁文件变化。

最初dotnet test误传--locked-mode未运行测试，改为RestoreLockedMode后运行；首套锁屏端口误调用SetSuspension(SessionLocked)触发明确拒绝，改用原SetSessionLocked，断言不改。失败日志/TRX均保留，最终11条红/绿使用同一断言。

## 官方行为与未验证范围

[BeginInvoke官方文档](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatcher.begininvoke?view=windowsdesktop-10.0)规定同优先级FIFO；专项既测FIFO旧/新排队只执行最新，也有逆序端口对抗测试，后者不代表WPF自然倒序。[DispatcherPriority](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatcherpriority?view=windowsdesktop-10.0)的Loaded优先级6先于Input5，因此只承诺**输入意图已经被处理/观察后，仍在外层Loaded队列的旧回调不再发出滚动请求**，不承诺抢先于尚排队的输入，也不撤回已经发给WPF的ScrollTo请求。[AddHandler](https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.addhandler?view=windowsdesktop-10.0)允许handledEventsToo观察已处理事件；实际路由、鼠标捕获、布局和内部滚动命令仍需Windows验证。

本批没有完整B组、混合DPI、阅读位置体验或生产验收通过的声明。原Windows生命周期根因、实际debug flags、MSVC精确版本和ABI命令缺口仍Unknown；无新的Windows或模型执行。

补充独审工具事实：独立/tmp专项11/11通过。该次使用临时CLI home首次启动SDK，输出明确报告已安装ASP.NET Core HTTPS开发证书；未执行trust命令，未另行检查或操作私人证书存储，因此实际存储变化Unknown，不能宣称证书状态未改变。原输出/专项TRX私有保全。后续应复用既有CLI home并显式关闭SDK自动开发证书生成；本批没有为核证书访问私人路径或执行清理/信任命令。
