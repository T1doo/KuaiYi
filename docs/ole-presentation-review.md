# E06 主浮钮与 OLE STA 分离候选接线

基线 `421ecfe92a731e298ff94113989535e800bf0969` 已获监督对 CaptureHost 隔离/GDI同步的限定签收。当前专项回到原 E06；代码与云验证已整理，**待监督独审后再推送，E06/P0尚未通过**。没有进入P1—P8，没有改变106整体用例的NotRun状态。

## 实际接线与责任边界

- App 的 `OlePresentationWindow` 创建、绘制和移动唯一真实浮钮HWND。接收进程没有可见窗口、假按钮或覆盖命中代理。红/绿及96×64DIP悬停提示是P0本地接线，尚无完整翻译面板、精致生长动画、位置持久化或设置。
- `OleReceiver` 的 OLE STA 创建原生 IDropTarget；`OleNativeBridge` 不实现FTM/IAgileObject/自定义封送。TABLESTRONG标准封送包只从自己启动的进程继承stdio接收；校验标准OBJREF头/IID/64KiB边界后，App独立registrar STA解封送并在App真实HWND注册。注册线程使用OleInitialize和完整Dispatcher泵；UI线程不做这些COM调用。真实跨STA HWND组合必须Windows裁定。
- Unicode有限格式查询仅On时发生；无async接口、async关闭或Start失败仍保留同步Drop内复制。协商成功才转独立STA。引用、EndOperation、marshal packet和cleanup均有实际调用；清理失败不交付。外部COPY/MOVE策略与原严格测试未删除。HTML/RTF/图片、UIA联合取词及完整产品协调器仍未交付。
- 复用原严格OwnedProcessSession到Core：实际后台Start、原启动task、持有child、原注册task、确认撤销均须终结。Query/Drop/cleanup固定1.5秒，启动10秒，实际退出等待2秒；registrar终结单次等待2秒。未知退出/启动/撤销保留owner并禁止重启；明确重启仍Off。进程kill只针对Start返回的held child；不按外部PID终止，不杀未经确认后代。注册STA不可安全强杀，阻塞时保留并限制复用。
- 父App直接写32字节只读共享门控的启用/epoch位，关闭与启用串行，Kill/pipe失败也先撤销新采集；receiver在查询、GetData、结果提交前复核。实际OS父进程、同根App/Receiver角色、会话、HWND owner另核。没有新DACL、系统权限或配置/receipt。
- 主输入为鼠标事件坐标钩子限定新按下 + INPUTSINK Raw Input移动/释放；Raw Input按下位不用于建立手势。外部原先按下或经过浮钮不产生自身手势。右键Off，忙时短按取消，移动暂停采集。钩子不消费事件、不注入源进程、无键盘监视/轨迹保存。只有自身明确操作授予OlePresentationConsent，其他OCR的On无效；隐藏/Off/模式变更撤销，暂停阻止新采集。注入输入/触摸/辅助技术仍未完成。低层钩子时限和静默移除是明确Windows待测限制。
- P0评审工作流串行：打开浮钮前需Off且旧OCR/CaptureHost退出、无已接受浏览器身份；浮钮存续时不开始OCR/截图，也不允许浏览器桌面确认，相关控件暂时禁用。此为P0协调边界，完整多模式产品coordinator尚未交付。自身拖动的DPI变化保持手势；其他工作区/拓扑变化关闭采集再重算。实际窗口行为仍待实测。
- 结果在AppDispatcher执行时再次核owner/ready/token/epoch/独立同意/模式/暂停，才建立单次localOnly身份并显示。无Provider接线或模型调用。仅自己拥有的可写byte数组确认清零；不能宣称字符串、OS或COM所有副本绝对擦除。

结构与官方平台依据见 [ADR0010](adr/0010-responsive-ole-presentation.md)。界面只显示有限线程/阶段元数据；不默认记录用户内容。状态显示原生App HWND线程、registrar线程和receiver事件线程，供实机核对归属，零线程号代表尚无该原生事件证据。

## 可执行证据及局限

新增 **48项**：21个接收/监督/手势用例（19个启动真实child；1个延迟启动的合成失败不造child；1个纯手势）、12个有限包/Unicode/终端Off边界、9个独立授权门控、6个角色文件篡改。专项50项含原11角色用例；另有9项授权测试。提取共享owner后原45项Worker启动/终结与CaptureHost严格用例先通过，随后全部保留在回归。

实际child运行生产OleReceiverServer/Client的继承stdio、writer、control、deadline及owned-lifetime；**后端、注册器、共享门控、阻塞/kill故障适配和pointer场景是明确测试替代**，从未解封送合成packet、运行WPF或调用Windows COM。测试验证真实pipe/child退出、晚启动/失败启动/已发布退出但启动未终结、注册原task或撤销未知时不复用、kill失败后独立control仍收到Off、迟到UI任务不提交、原payload清零和非法COPY/cleanup/实例/UTF拒绝。合成Release阻塞不冒充真正IUnknown Release。

最终离线 **Core489 + Provider77 =566/566**，Node **63/63**，无跳过。24个托管工程locked Release零警告/错误；最后输入Windows接线仅编译/重建，不把此前共享逻辑通过冒充该Win32新接线实测。身份源静态6项通过。Provider只用本机测试HTTP服务，无真实凭据/模型。前一轮GDI7项为历史已验证证据，本轮没有重复执行或计入新增项。

保留测试编译CanSend参数错误；首轮19例3失败（清理未等待合成注册原task、cleanup重复观察时未接受已断言的DecoderFallbackException、把localOnly误当成禁止新本地request）。按真实合同改用等待原task、保留拒绝/零交付断言和对已同意origin的CanSend=false核对，后19/19、扩展50/50。共享view构造清理首次CS8602已修；一次缺Linux targeting参数的NETSDK1100来自工具输出记录，原日志名被后次调用复用，明确不当作原始日志。第一次archive保密检查把runtimeconfig误判为config.json，改为准确保留文件名核对，所有摘要/PE/排除native/fixture断言仍执行。原失败与工具观察分别保存，未删除旧测试或弱化核心断言。

新独立审阅包路径、精确摘要、逐项实际PE头和文件摘要见 [summary](test-evidence/2026-10-11-ole-presentation-summary.json)。含5个实际x64 PE apphost、39运行/资产文件、2原生桥源及manifest；无fixture、配置/receipt、真实秘密或编译原生DLL。框架依赖，**不是安装包，缺桥DLL不能运行拖放，缺Capture/OCR/Shell DLL不能完成其他平台能力**。三个之前审阅ZIP摘要保持不变。

复跑：`build.ps1 -Scope Managed`、`test.ps1 -Suite Offline`、`test-identity-source.ps1`、`stage-ole-presentation.ps1`。角色完整摘要要求17→23，旧receipt完整清单也明确拒绝；实际旧17项清单没有被自动升级。生产项目不引用OleReceiverFixture。

## 最小 Windows gate 与运行方式

此云Linux无MSVC/Windows SDK/交互桌面；原生桥真实构建、COM、命名映射、输入、焦点和DPI全部 **NotRun/Blocked**。必须在获授权的Windows测试桌面，用公开fixture执行；本轮不操作用户连接桌面。

```powershell
pwsh -NoProfile -File scripts/build.ps1 -Scope All -Configuration Release
pwsh -NoProfile -File scripts/stage-identity-probes.ps1 -Configuration Release
pwsh -NoProfile -File scripts/pack-probes.ps1 -Configuration Release
& ./artifacts/identity-probes/install/KuaiYi.App.exe
# 在评审App中打开真实浮钮；初始Off，明确点击后才On。只使用公开文本。
& ./artifacts/probes-win-x64/KuaiYi.OleFaultSource/KuaiYi.OleFaultSource.exe `
  ./assets/fixtures/selection-public.txt Sync Copy ./artifacts/windows-e06-source-sync.jsonl
```

以上源码构建/暂存不签名、注册、安装或更改权限；原生构建失败先修，不以托管ZIP代替。已有故障来源暂存脚本不是新App目标的自动验收器，不能通过旧OleProbe监督计数证明本浮钮可用。

1. 实测App HWND owner/UI native线程、独立registrar STA、远端native目标STA。真正源DragEnter/Over/Leave/Drop到远端，App浮钮移动后仍真实命中；注册前/后、重复启动、关闭全部核对。
2. Sync/Copy和CopyMove成功且文本相同、源不删；Async/AsyncDisabled/StartRejected分别验证真实协商、同步回退和LIFO清理。MoveOnly/Off下Query/GetData零次。真实GetData/Query五秒阻塞期间，从新的物理按下移动实际浮钮并取消/右键Off；确认原始拖选、经过和事件积压不误触。测钩子回调时间、源捕获、Raw Input/低层钩子顺序与失效恢复。真实IUnknown Release另需真正原生故障来源/跟踪，不拿原适配当证据。
3. 只杀owned receiver，App HWND和画面保留。实际终止/启动settlement/注册撤销未确认时明确保留owner，禁止重启；晚UI回报、原请求关闭或替换不能提交。确认后明确重启Off。跨进程共享门控在pipe失效/kill失败时确实拒绝新Query/GetData；已进入阻塞调用仍不交付。
4. 双屏负原点、100/150/200%DPI、工作区变化、边角、前台浏览器/PDF/Markdown、权限差异、输入焦点和来源取消恢复。采集实际像素、窗口轨迹和帧时延。48DIP/96×64提示不代表P1动画品质已验收。

若注册组合或捕获期间输入在Windows失败，E06继续Blocked并保留准确失败证据，再修结构；绝不改验收标准。当前仅交监督审阅可编译候选，不宣称真实跨应用翻译、安装、动画或P0退出完成。
