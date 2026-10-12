# P0—P4 实际缺口复核与文字 Alpha 发布链

启动/远端基线为父限定签收的 `dc70f5e529cdbafc588d7419191090e6f0c683ad`，工作区原干净，项目 cwd 没有活动 git/dotnet/pwsh/node/cmake 写入任务，只有本轮写入者。仍仅使用原 KuaiYi 云环境；用户 LAPTOP-USISIS19 的连接不等于执行授权，本轮没有访问其桌面。

完整对照 SPEC16.2—16.6 的30项原任务及18.2交付合同，区分已有源码与真实平台结果：

| 任务 | 当前实际模块及剩余门槛 |
|---|---|
| P0-01 | global.json、集中版本、27原锁、bootstrap/build；原生 SDK/MSVC/CMake 准确版本仍待 Windows 已有工具核查，本轮只补 Alpha 独立锁定发布图 |
| P0-02 | CaptureBroker / CompatibilityProbe / compatibility-matrix；全部真实应用/视图数据仍 NotRun |
| P0-03 | Windows NativeDesktop、App OverlayPresentation、OleNativeBridge/Receiver；实际 HWND/焦点/OLE/E06 未验 |
| P0-04 | Core OverlayGeometry/Frames、MonitorTopology、实际 GrowthHarness；混合 DPI/负屏/工作区实际验证未验 |
| P0-05 | identity脚本、ImageWorker/OcrNative、ShellExtension；原生构建、身份/OCR/现代菜单/签名安装未验 |
| P0-06 | CaptureHost/CaptureNative、NativeHost/扩展协议；离线协议已验，Windows取图/注册/完整平台路线未验 |
| P1-01 | Core / Contracts / Providers / App 分层与领域状态，已构建；Windows实际 UI 运行未验 |
| P1-02 | ShellWindow/NativeTray/SessionSingleInstance/SettingsWindow；红绿/拖动/托盘/实例实机未验 |
| P1-03 | SettingsStore/Coordinator/恢复/OverlayPositionMapper；已有自动化，实机 IO/位置未验 |
| P1-04 | SecretFileStore/WindowsSecrets/设置掩码事务；合成加密与逻辑已验，实际DPAPI/ACL未验 |
| P1-05 | ShellLifecycle/RuntimeGate/锁屏与清理接线；逻辑已验，真实session/关闭行为未验 |
| P1-06 | 类型化错误、脱敏stderr与独立测试Provider；本轮维持正式依赖图不引用Mock，真实桌面异常恢复未验 |
| P2-01 | 强类型像素/DIP、MonitorTopology、LayoutPlanner；逻辑已验，实机拓扑未验 |
| P2-02 | NativeDesktop/OverlayPresentation/区域命中；真实透明输入/阴影未验 |
| P2-03 | GrowthController/Frames、实际生产-overlay探针；真实呈现/帧率/视觉品质未验 |
| P2-04 | OverlayResults/工具栏/版本回看/滚动保留；逻辑与接线已有，真实完整操作未验 |
| P2-05 | 原生拓扑通知、拖动冻结/重新规划；跨屏/DPI/任务栏/移屏未验 |
| P2-06 | 系统减少动画/高对比度、主动阅读/键盘入口；Windows辅助功能未验 |
| P3-01 | SelectionMouseHook/InputBridge、候选epoch/去抖；源逻辑已验，真实hook未验 |
| P3-02 | MTA Broker、OwnedProcessSession、600ms拒绝/重启；实机COM/UIA卡死未验 |
| P3-03 | SelectionTextPolicy/来源/排除/去重；逻辑已验，应用真实正确率未验 |
| P3-04 | ClipboardHost、授权/sequence/owner恢复；逻辑已验，实际STA/Ctrl+C/格式未验 |
| P3-05 | HotkeyHost/录制/事务替换/占用器；逻辑已验，真实占用及输入队列未验 |
| P3-06 | SelectionFlow→TextTranslationCoordinator→实际接收→Overlay；生产接线已有，来源/焦点桌面闭环未验 |
| P4-01 | ApiProfileMapper/设置地址/参数/能力/公开测试；两消费者/迟到队列已限定签收，真实设置UI未验 |
| P4-02 | OpenAI Chat HTTP/SSE/JSON，真实回环合同已验；授权真实服务/模型未验 |
| P4-03 | Anthropic Messages 独立适配与200内error，真实回环已验；真实服务未验 |
| P4-04 | 原总预算/阶段/资格/调度/取消/重试/队列；源码专项已限定签收，实机队列/网络条件未验 |
| P4-05 | 四模板/安全自定义/会话风格/版本N/三版本/复制接线；实机操作及真实文本J质量未验 |
| P4-06 | 生产选区→真实协议→生长→收起回看源码接线已有；本轮补实际自包含Alpha交付/受控启动，真实Windows闭环仍是下一门槛 |

本轮云内真实工程缺口是交付可执行性：此前审查 ZIP 全是 framework-dependent，没有可复现的文字 Alpha 自包含发布合同；Windows suite 入口直接抛“未实现”，也没有正式的有界公开本地启动入口。本轮新增 stage-text-alpha.ps1、独立runtime/RID发布锁、完整包验证、run-text-alpha/desktop入口并接 test.ps1 的明确Windows执行参数。没有把P7安装器、P5/P6业务提前冒充已交付。

第一次SC restore真实 NU1004失败保留：传 win-x64 给原无RID库，原锁拒绝图变化，27旧锁无改。新增 KuaiYiTextAlpha 条件下13独立锁，原NuGet条目逐个保持；SDK10.0.401/runtime10.0.12/ridwin-x64固定。初始化后再真正locked发布，所有宿主先独立publish，重复runtime/依赖只有摘要一致才共用，完整来源归属入manifest；冲突拒绝覆盖。GUID目录/ZIP永不选历史输出。Assets/托盘图标和公开fixture入包；Runtimeconfig includedFrameworks、真正runtime DLL/PE、WPF文件和正式无Mock依赖图均校验，不靠SelfContained标签自证。

所有新包是SC多文件/不裁剪的托管文字Alpha，test-provider隔离；没有Capture/OCR/Shell/OLE产品原生DLL、身份/安装包/注册。未来已授权Windows执行只需Windows11x64、普通已解锁Default交互桌面、PowerShell7、受控公开fixture应用，无需为此包安装.NET/SDK；源码/原生构建仍单独需工具链。本轮没有Windows实际运行。启动前验证每文件/角色/运行时，拒绝已有快译实例；默认仅预检，只有明确Execute才允许有界启动自己的进程。取词仍用户明确启用/公开选择，既有不可变回环授权/None认证/TEST模型/Off/身份/阶段资格不变，真实模型与凭据入口不使用。结束仅清理实际持有进程树；正文不写报告，源码正确性子比较不能把整体106改Pass。

断连后05:13:07.806097Z实际命令成功，dc70/三份当时新增文件/零活动构建保全；未重复健康进程、未扩权。父再次索取状态时已完成独立锁定发布和普通回归；最后发布实际开始05:34:45Z，原恢复证据与全部失败/发布日志保留，不伪造未打印的最后命令UTC。

Directory.Build.props及test.ps1工程入口变化后已实际普通CI：1836 .NET /63 Node、27managed Release零警告/错误，原全部案例保留；13 Alpha锁与27普通锁分开。WindowsUI/跨应用/DPI/DPAPI/焦点/实际模型/原生/安装/远端CI仍NotRun，P0/E06 Blocked、P1—P4 InProgress，106整体NotRun。本轮包验证拒绝实验与独立文件/ZIP核验见summary，不作为新平台验收。

新增 runtime packs 为 .NET10.0.12 Microsoft.NETCore.App.Runtime.win-x64、WindowsDesktop.App.Runtime.win-x64、AspNetCore.App.Runtime.win-x64；实际nuspec均MIT，AspNetCore包requireLicenseAcceptance标志true如实记录，标准依赖恢复没有新SDK协议/对话/信任操作，不称未下载依赖。WindowsSDK/MSVC/安装器不安装。

依据：[Microsoft publishing overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/) 与 [dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)。自包含包含对应.NET runtime，但不自动提供或验证产品的原生Windows集成；实际命令/文件与这些说明分别记录。
