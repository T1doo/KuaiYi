# Native诊断worker/host适配：离线验证，Windows未运行

基线 `93b9a5806ba76437b90b26348b10810eb10fc09b`。仅独立tools、默认Off、最多两次顺序独立请求；未接生产SelectionBrokerClient，EOF+自然exit0原门不变。没有新增Windows/UIA/Job实验、模型、费用、凭据、系统权限配置、包安装或部署。环境断联提示后实际同一保存workspace仍可执行，最近完整脚本成功，无重复实例。

## 移除合成成功假设

host必须由调用方传入DiagnosticRequest（PID+启动UTC ticks+HWND、down/up物理像素、explicit/gesture模式）。固定42/123456/99和三字节样本仅存在test fixture；host无硬编码来源/文本。测试调用方显式提供样本matcher，native默认没有matcher，也不返回文本或文本hash。Match为NotRun/Matched/NotMatched；无文本或无matcher为NotRun，不能把未执行比较写成false mismatch或正确翻译。合法SoftTimeout/Empty/Sensitive的真实Outcome保留，transport Completed与捕获Success分开；Stale立即终止、不复用。

诊断wire升级v2，仍96字节固定、32字节payload cap。Begin的25字节为down/up四个int32、explicit+Job-attested位、父端原始QPC origin；真实PID/start/HWND沿用原固定槽。v1拒绝；旧unknown-version测试仍断言拒绝，值改成99以保持新v2下的同一安全语义，没有删除或弱化断言。原57纯用例不变。InitializationFailed有独立帧：无MTA/API-ready位、无payload、无Quiescent；初始化前已软到期保留SoftTimeout且Initialization=NotRun，不假装Ready。实际初始化失败Initialization=Failed。合法Ready仅在CoInitializeEx与CoCreateInstance均成功后发送。

host按原250/600ms/120ms Idle检查。natural nonzero不再只认23：在请求父端停止之前实际观察到的自然退出区分NaturalZero/NaturalNonZero，非零导致Crash transport状态；仍不是异常指令/根因证明。未退出时父端请求Stop标ParentRequested，表示控制意图而非Kill完成或已证退出原因；真正HasExited和IO/start/stop结算仍是发布门。自然退出与请求退役相邻时的竞态不推断未知因果。正常round关闭stdin、检查尾部EOF、等待实际退出，失败/取消/600ms仍终止自有子进程。

stderr由独立诊断排空线程处理，固定256字节缓冲，逐批立即擦除，累计超过4096触发退役；继续排空到自有进程退出，防止阻塞，不转发/保存正文，不发布任意退出码/异常正文。stderr task未完整结算禁止释放owner/summary，迟到start也随后排空；初始化未创建child的负证结算路径保持不伪造退出。

## 请求作用域与实际释放代码

`tools/KuaiYi.SelectionReuseNative`是新Windows x64 C++20诊断exe，不进solution、workflow、安装包或production reference。主线程不创建窗口；仅optional MTA apartment及IUIAutomation跨请求存活。每次Source/手势传入，不预热/重试/请求缓存；协议身份元数据（同PID/start/HWND、上次nonce、最多2计数）是必要控制状态，不保留root/focus/hit/pattern/range/属性对象或text。

SelectionCapture.h从冻结SDK诊断采集复制：来源/前台/启动时间/排除/完整性/初始密码、8层root/PID绑定、共64候选、每候选密码、64ranges/256rects/100002 UTF16哨兵/50000标量、几何/Unicode及最终身份门保持。请求内借用persistent API代替重复COM初始化；request_scope把所有明确接口holder包装，真实调用Release后才减少owner；BSTR先SecureZeroMemory+SysFreeString，SAFEARRAY销毁/解除访问失败标清理失败、禁止Quiescent。所有目标调用位于请求函数中。

自有wstring和UTF8副本用secure allocator+scope string：写入文本前强制heap reserve以避免SSO union残留，移动源清零，resize旧capacity再擦除，heap deallocation全块擦除。release/zeroization失败使ledger clean=false，不发静止确认；normal/exception/early-timeout离开目标作用域后才检查ledger settled。实际UTF8编码副本仅验证后擦除，不发送text/hash。完成后先销毁range/pattern/root/focus/hit/walker/临时API借用、所有字符串和numeric属性容器，再发Completed和Quiescent；Stale只终态结果并退出，不走第二请求。

所有者的Release/字符串擦除逻辑是实代码，但**Windows COM引用释放尚未执行验证**。计数证明显式holder/自有内存作用域，不能证明UIAutomationCore/provider内部无缓存/残留，也不能替代原生产每请求进程隔离。整个native exe退出时先释放persistent API再CoUninitialize；阻塞/崩溃可能不执行析构，仍需父端Job+真实退出门。库/OS堆和目标应用已有文本不在“自有缓冲已擦”证明范围。

## 父端骤死Job门：已编码，未执行

父端创建unnamed Job（NULL security attributes默认不可继承），显式清除并检查HANDLE_FLAG_INHERIT，设置JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE，不开breakaway、不复制/传给child。父端独持SafeHandle直到整个owned child真实退出。Start返回后AssignProcessToJobObject并用该**同一Job handle**验证IsProcessInJob；全部成功之前不发送第一个Begin，不允许目标访问。worker等待stdin/Begin、核attested位和自身job membership后才初始化MTA并调用目标；它没有Job handle，不能独立验证父Job限额，依赖受信的私有stdio控制父端，不是可接受任意外部sender的公开服务。

creation/assignment/membership/不可继承检查失败：停止、终止并实际结算自有child，不降级为无Job运行。shared QPC频率与Stopwatch.Frequency也需一致，不一致无fallback。父端在assignment之前骤死，worker还未收到Begin，只做bounded packet read，stdin关闭后退出，不调用target；assignment之后骤死时最后Job handle关闭由kernel终止job。这里是**设计/源代码保证路径，实际父端强杀/Job行为未验证**，没有设置新系统权限/全局策略。

官方依据：[CreateJobObjectW](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-createjobobjectw)说明NULL attributes的不可继承与NULL name的unnamed行为；[Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)说明KILL_ON_JOB_CLOSE在最后handle关闭时终止关联进程；[AssignProcessToJobObject](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-assignprocesstojobobject)说明所需process handle访问与嵌套限制。存在外部Job/嵌套约束导致失败时仍拒绝，未假定所有Windows运行器可成功。所谓exclusive限于本代码不命名/继承/转移handle，不声称能阻止同权限恶意进程复制handle。

## 实际验证与构建缺口

运行 `pwsh -NoProfile -File tests/scripts/run-native-reuse-offline.ps1`：实际xUnit纯57/57（18Fact+39InlineData）、合成host42/42（原26+新增16）独立输出，managed/test runner构建0warnings/0errors。新增状态/InitFailed/Init-before-soft、任意natural退出、父端watchdog退役、stderr flood擦除、Job fail-closed policy及真实输入编码/Stale终态；Job用例仅注入布尔，不运行Win32。全部child是Linux合成fixture，不调用UIA。

诊断worker共用native请求RAII **30/30**、共用native packet layout **21/21**：g++ C++20/Werror +ASAN/UBSAN通过，模拟接口实际AddRef/Release调用、normal/throw/early-timeout顺序和自有heap/encoded buffer清零；不等于COM实测。LSAN因当前ptrace限制仍Blocked。源对照 **1/1**，归一化字符串类型、显式GetAddressOf输出地址和persistent API借用后完整capture body与冻结原门一致，防止拷贝时删门；不替代编译/ABI/提供者测试。新增PowerShell脚本首次因sanitize逗号参数parse错误失败，正确引号修复后完整执行通过，不改测试门槛。

**完整native Worker MSVC/Windows SDK构建Blocked/NotRun**：当前Linux仅g++，无cmake、cl、MinGW或Windows SDK；不装依赖、不伪造SDK/stub编译通过。`tests/scripts/build-native-reuse.ps1`仅在已就绪Windows x64环境configure/build、/W4 /WX /EHsc、原per-monitor manifest；不会运行worker/UIA/Job。该脚本本轮只源审，未启动Windows构建或实验。所有native Windows ABI、CUIAutomation、Job/父端死、真实COM释放与performance仍未验。

生产src/SelectionBrokerClient、workflow、solution、包装、安全设置不变。原Root224ms/Focus43ms/AutomationCreate5ms不能证明固定进程初始化成本，更未证明复用取词性能收益。旧SDK两次5/6、managed AV/HardTimeout、两次HTML Correct0、GetData代码/Actions `220060f813a3640487d593920a1b1824aa779a00`全部保留；P0/物理划词/Win11/密码提权/PDF/Markdown/DPI/安装无新通过。下一步由监督方按完整native构建缺口和源码独审决定，不能自发Windows/UIA验证。独立审查已发现并要求修复原退出Wait结算缺口，修正和复审见下。

源码主审另发现WRL输出参数写法与新request_com不兼容的明确编译缺陷：10处接口输出改为显式GetAddressOf，未改目标门。新增实际T**写入、非空输出拒绝、Release及scope结算4断言。修正后重新完整运行离线脚本：57/42/30/21/1全部通过，managed构建0warning/0error；Windows SDK完整构建仍Blocked/NotRun。

独立只读审查另发现正常退出及early-EOF 20ms宽限的原始WaitForExitAsync任务在包装超时后未被持有。已统一先登记原任务观察者，再包装等待；summary/finalize沿lateIo门等待原任务终态。新增两组实际合成进程屏障测试（正常路径、early-EOF宽限路径）：进程确实退出、包装已超时但原观察未结算时，Summary仍null、TrySettle=false、HoldsOwnership=true、CanReplace=false，释放屏障后才确认。重跑managed脚本纯57/57、host44/44、0warnings/errors；原42项未删改，新增2项。原生30/21/1在此前同一原生源码通过，本次仅host改变。

registration_review独立只读复审确认上述原任务保留修正可接受，NativeWorker/RequestLifetime/packet/Job/host已读范围未发现其他静态阻断；结论仅为离线源码审结。完整MSVC/Windows SDK编译、真实Job/父端骤死、UIA/COM释放与性能仍为Blocked/NotRun，生产不接入，Windows/CI不启动。所有变更仅本地正常开发分支提交，未推送或发布。
