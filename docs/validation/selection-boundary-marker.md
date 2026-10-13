# 非持久崩溃边界取证（诊断，不是兼容验收）

起点是干净开发 HEAD `66761a2b7470fc2a82d70f6c04cbc5a17c2511bd`。前次真实六调用源码 `2270d00096d653ac63d896ec58e7872a8cc0ab0e`、run 38377252994/job 115228634932，仅 Observed 0/6，四个原进程退出码为 `0xC0000005`；尚无具体故障调用或模块。不得据此换 STA、强制释放 COM、恢复访问违规进程或放宽保护。

## 实现与安全边界

只在 CompatibilityProbe 的 DiagnosticOnly 模式为 SelectionBrokerClient 启用边界标记。每请求以已有随机 requestId 创建 Local 会话命名、逻辑 64 字节的系统内存映射；不关联磁盘文件，不更改系统权限，不保存用户文本、路径、坐标、指针、异常消息或 dump。父保留 mapping/view/已 Acquire 的指针到原持有子进程真实退出与启动/清理结算。子仅通过新增固定参数 `--boundary-marker` 打开同请求映射。默认生产调用仍为原九参数，没有映射或标记。

[MemoryMappedFile.CreateNew 官方说明](https://learn.microsoft.com/en-us/dotnet/api/system.io.memorymappedfiles.memorymappedfile.createnew?view=net-10.0)支持非持久系统内存映射，命名跨进程映射仅 Windows 可用。映射初始化/打开的预期失败只使诊断不可用；不伪造成功或借用旧请求。Ready 表示父映射建立，不证明子打开成功；未发布记录为 null。

固定头含版本与 requestId。单 MTA writer 在对齐 offset 24 的 64 位单元，通过 Interlocked.Exchange 一次原子发布固定 enum、Enter/Returned、序号和校验标识；无锁、线程、队列、流、同步 flush 或 stderr 写入。父原子读取并校验请求、版本、保留字节、固定枚举、位置和 1—16384 序号。损坏或陈旧标记仅为 null。同步 MTA、原 UIA 调用次数和对象生命周期不变。

括住 Source/Own 完整性 SID/子权限计数/子权限值的原生指针读取、GetSelection、GetBoundingRectangles、GetText；另记录 Capture 进入/返回、Metadata 准备进入/返回、双帧写入进入/返回以及 Main 即将返回。`MainReturning/Enter` 后还有标准流与映射 Dispose 及运行时退出，不能把该阶段后发生的崩溃称为 UIA 调用崩溃。标记是 LastReachedBoundary，不能视为故障指令或模块实证；两个标记之间未括住的操作仍可能是原因。

父只在原 WaitForExitAsync、绝对截止检查、真实非零 ExitCode 接受分支读映射，读后再复核取消、600ms 和 disposed，再发布独立 LastReachedBoundary。真实非零退出的 LastObservation 保持 Unknown。exit 0 完整 BrokerFailure、无效协议、硬超时、取消、Kill 后退出和暂停不得借用该边界。所有入口清空上次结果；原三次失败/60 秒、暂停 30 秒保留。父的已有 ownership 锁只保护指针读取/释放，不在子 UIA 发布路径新增锁。

## 验证计划与证据边界

纯测试使用真实匿名非持久映射验证原子协议与 Dispose；进程测试以实际持有 child 的退出为准，但阶段发布来自明确的测试回调，不等同于 Windows 命名跨进程映射或真实 UIA 验收。覆盖陈旧/损坏头及原子字、初始化不可用、Enter/Returned 两侧原退出、迟取消/600ms 拒发、exit 0/协议失败不借用、原启动延迟期间持有映射、原子进程清理后释放、下一请求清空与暂停规则。摘要仅独立投影固定字段；有边界时必须是 DiagnosticOnly、Ready、已清理真实非零 Crash 且无完整 BrokerObservation。标记不得出现在兼容性 Pass 证据中。

纯测与精确冻结独审通过后，仅运行一次现有 Windows 免费有界六场景诊断，15 分钟 job 上限；不重跑完整 120 项，不重跑 GetData。250ms capture 和 600ms 父绝对预算保持原值，映射操作开销计入原预算；不以纯测或 CI success 标为取词通过。

GetData 成功代码及 Actions 源码 SHA 独立保留为 `220060f813a3640487d593920a1b1824aa779a00`（run 38347142627/job 115131066822）。P0 整体、Win11 物理划词、PDF、Markdown、多屏 DPI 与安装卸载均没有新增通过结论。

本地正式 Contracts 构建及实际 NativeDesktop/Broker/client/probe 源码类型检查均 0 warning / 0 error。原 xUnit 断言直接执行 **196/196**（忽略日志 `artifacts/windows-selection/boundary-marker-contracts.local.log`），PowerShell 摘要边界 **57/57**。新增测试还验证父 Dispose 在原退出之后、交付之前发生时不迟发边界且释放映射。Linux 匿名映射及测试回调不等于 Windows 命名跨进程或 UIA Pass。新 Windows 六场景尚未运行。
