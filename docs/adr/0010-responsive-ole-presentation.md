# E06: 主呈现 HWND 与远端 OLE 对象分离

状态：候选生产接线已实现并交叉编译；Windows 关键假设 NotRun，待监督独审。基线421ecfe92a731e298ff94113989535e800bf0969。SPEC 6.2/6.3、11.3、P0-03、E01—E06 要求：真实浮钮命中、同步来源不全部拒收、主 UI 可拖动/取消/Off，阻塞 COM 不在呈现线程。

唯一浮钮 HWND 由 App 的 WPF STA 创建、绘制、移动并保留。无接收进程可见按钮，无透明命中代理覆盖按钮。可重启 OleReceiver 的 OLE STA 创建 native IDropTarget。它通过 MSHCTX_LOCAL 标准封送导出 IDropTarget；App 专用 registrar STA（OleInitialize、完整 Dispatcher 消息泵）解封送并把这个远端对象注册在本进程真实浮钮 HWND 上。Registrar 无 WPF 控件访问；解封送、Register/Revoke/Release 不在呈现线程。接收进程仅做有限查询、COPY、原同步/协商异步提取与清理、私有 IPC 状态。主 UI 不进行跨进程同步等待。worker 被终止时浮钮仍属于 App；恢复需实际退出、启动终结和撤销注册均确认。

使用 native IDropTarget 保证标准 COM 对象的 apartment 归属，不依赖托管 CCW 的敏捷性猜测。只接受自己创建的接收进程私有通道和 IID_IDropTarget 标准 OBJREF，不消费外部任意 COM 封送包。生命周期保留原 apartment/引用、TABLESTRONG packet、注册代理及实际子进程 owner。未知退出或 registrar 未终结一律禁止复用；旧反馈/数据须通过实例、epoch、transaction 和固定截止检查。

同步源的 DoDragDrop 可能在 Drop 等待中保持鼠标捕获。浮钮自身移动/释放使用 INPUTSINK Raw Input。复核发现，处理 Raw Input 按下时读取当前光标不能证明原始按下位置：积压的外部按下可能在光标已经过浮钮时才被处理。因此 Raw Input 的左右按下位全部丢弃；仅鼠标 WH_MOUSE_LL 的 MSLLHOOKSTRUCT.pt 原事件坐标、真实 HWND 命中及按钮区域共同建立自身手势。物理新按下才参与此 P0 手势；注入输入/触摸/辅助技术路径尚未交付。钩子不消费/改写来源事件、不注入来源进程、不注册键盘；原回调被强引用，Unhook 未确认时保持失效 owner 和窗口，不能假报清理完成。没有输入内容或鼠标轨迹持久化。

外部拖放身份只来自 IDropTarget，经过按钮、外部源原先按下的左键不能推断为 DragEnter。右键是明确 Off/取消，左键短按在提取中取消；自身移动暂停采集并增加 epoch。独立 OlePresentationConsent 只由浮钮自身明确启用授予；其他本地 OCR 工作流的 On 不授予 OLE。窗口隐藏/Off/模式变更撤销，设置/截图/移动暂停阻止采集。48DIP物理锚点按 PMv2/current monitor 处理；后台非激活与真实 DPI/焦点/命中仍需实测。低层钩子运行在泵消息的 App线程，只做按下处理；官方指出超时可被静默移除，现有代码不能检测此情况，须实测回调时延/输入积压/关闭，未证明适合完整产品。若实际组合失败，保留该 gate，重新设计输入通路，不能把无响应签 Pass。

Off 另有32字节实例共享门控：父App直接写入对齐原子epoch/启用位，接收方只读并核父PID/实例；不依赖控制管道或卡住的 COM 返回。负stamp永久撤销。父端启用与取消串行，避免迟到启用覆盖Off。控制管道仍独立处理Off；杀失败保留实际子进程与门控，注册原任务/撤销未确认也阻止重用。Windows命名映射/原子跨进程行为尚未实测，不改变DACL或安全策略。

最小 Windows 裁定门槛：

1. 三个实测线程归属：App HWND UI、独立 registrar STA、远端 native IDropTarget/数据 STA。用无隐私源确认真实 DragEnter/Over/Leave/Drop 到远端，而浮钮 HWND 属 App，注册发生在独立 STA；移动真实 HWND 后仍命中。官方 RegisterDragDrop 文档没有直接证明该跨 STA HWND 组合。
2. 无 AsyncCapability 的真实同步源正常 Drop COPY、GetData/Release阻塞；阻塞期间本地新按下浮钮，事件坐标钩子/Raw Input确实收到并移动真实 HWND，右键Off/取消继续执行。积压的外部按下、经过/快速离开不能变自身手势；特别验证源 DoDragDrop 捕获期间路由与钩子时延，不能以普通合成 pointer 或按钮动画代替。现有故障来源的GetData/Query阻塞是真调用；Release故障适配仍属显式注入，真实IUnknown Release阻塞证据另需补齐。
3. 超时终止自己的接收进程，真实浮钮 HWND 不消失；旧代理撤销和所有拥有的实际进程/启动任务终结后才能重启。杀失败/未知退出保留 owner，主呈现依然可操作，旧结果不能复活。
4. 双屏负原点/100—200%DPI、边角、不同权限/前台阅读器、源捕获与焦点、重复拖放、撤销/引用清理。真实像素/帧时间/动画和来源恢复另测。

5. 共享门控在真实进程中先于管道/杀失败生效；Off/锁屏之后新Query/GetData为零，已经进入不可取消调用的内容不提交。关闭与启用竞争、延迟注册、断流及晚结果不能复活。原生桥MSVC实际构建是以上实验前置条件。

依据：[RegisterDragDrop](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-registerdragdrop)、[CoMarshalInterface](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-comarshalinterface)、[CoUnmarshalInterface](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-counmarshalinterface)。文档保证标准封送和注册线程须泵消息，不能推导 Windows 组合及捕获输入已经通过。Linux 编译/私有 IPC/实际子进程测试只验证共享监督；E06/P0 不能标 Pass。

输入依据：[Raw Input后台接收](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input)、[同类设备单窗口注册限制](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices)、[低层鼠标钩子线程/时限](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)、[原事件per-monitor坐标/注入标志](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct)。这只是实现依据，不能替代E06桌面证据。
