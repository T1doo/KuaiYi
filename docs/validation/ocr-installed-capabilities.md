# 现有 OCR 菜单的已安装能力发现

父 `1ac32bede4ce7a9415c22b676c08ba071eb67412`、tree `d68a503cf353a937a69f0b93ae222b200e47833b`。先核实际文件、clean Git、单 worktree、唯一 root 写入者及默认网络远端祖先，建立 `dev/ocr-installed-capabilities`；UNKNOWN 旧实例不恢复。`ocr_capabilities_review` 只读独审，不执行 SDK、网络或写文件。

现有 Shell 的“仅提取图片文字”先调用隔离 Worker 能力发现，再使用不可编辑的已安装语言 ComboBox；替换原手输语言框，显示本机语言数、真实报告的 OCR 尺寸上限与当前截图尺寸。选择语言后才运行本地识别，仍需只读文字预览、四种风格及既有目标/模型/费用提示后的显式确认，才交既有文字 Coordinator 发送文字。普通截图仍只作本地预览；能力发现不携图、不发 HTTP、不安装语言/组件，也不启用视觉自动发送。

原 `NativeOcr.Diagnose` 由实际 `Program` 接入原 inherited stdio 私有 Worker loop；新增 `ImageDiagnose`/`ImageDiagnostic` 只走原 32-byte nonce hello、随机 requestId 与完整 job(session/version/epoch)，不是新增外部网关命令或仅新增 CLI。两种帧沿原 64 KiB JSON 上限，在分配 payload 前拒绝越界；严格拒绝未知 JSON 字段、错角色/身份/job。报告限制 256 个语言、每项 64 字符、ASCII 标签及不区分大小写去重；包身份文本有界。成功报告必须为 Windows、有效本产品 ImageWorker 包身份、正尺寸；失败不能夹带能力。空已安装语言明确阻断为 LanguageUnavailable。原 NativeOcr/PackageIdentity/C++ 查询实现不改；识别时仍会再次查询和验证真实能力。

能力快照复制为不可变语言集合，绑定原图对象、来源/版本、设置版本及完整 canonical 请求身份。仅同一快照引用可消费；原图/设置/模式/On/隐藏/锁屏/epoch 改变、取消及排队迟到均拒绝。识别在创建新请求前检查已安装语言和本机最大边长，并保留原 2560 像素、4 MiB 本地处理限制；超限提示重截，不先试发请求。旧快照取消只 StopRequest 自己的身份，不误停后继 C；延续父级 B 准入异常和无条件 finally 终结修复。

能力查询复用原一次性客户端/进程所有权、15 秒、启动/退出/原 I/O 结算、缓冲清零及拒绝重复调用合同。Worker 对原非协作诊断任务与原 control read 都 join；令牌或外层等待取消不能冒充原任务结束。父进程仍按原 owned-process 门终止不响应的子进程；原读/写/flush 未结算时保留 owner、拒绝新查询，原字节借用结束才清零。成功能力只在原 Worker 清理确认后交付。

| 原始执行 | 结果与边界 |
|---|---|
| r01 既有 Worker/OCR | 117/117；原传输、识别及终结合同 |
| r02 初版能力合同 | 48/48 |
| r03 App | 0 警告 / 0 错误 |
| r04 私有协议测试编译 | CS8752、CS9174；0 执行，无 TRX。精确失败测试源码和原 log 保留；修为显式 IpcFrame 与 Array.Empty<byte>，未降低断言 |
| r05 能力/协议 | 66/66 |
| r06 重入/限制扩展 | 76/76。之后修正新增参数夹具的 default 分支，确保三个非法成功报告用例实际测试 Failure=None；生产未改。r06 该中间测试源码未单独保全，不能重造或宣称精确源码可回放 |
| r07 最终完整 Provider | **1575/1575 = 父 1499 全保留 + 新 76**，0 失败/跳过；最终源码绑定此轮，独审另比较 testName 多重集 |
| r08/r09 最终 App/Worker | 两项 Release 交叉构建各 0 警告 / 0 错误 |

新增 58 项 discovery 与 18 项 protocol 测试执行实际生产 Imaging/Worker DLL。明确合成的 Windows 能力/识别结果、调度及流阻塞夹具用于成功、不可变性、语言/尺寸消费、错误包/缺失能力、取消/迟到/设置/来源/模式、清理未确认、A/B/C 重入与两个原任务 join 合同；真实 Provider/Kestrel 双协议核验显式确认后只含文字，阻断路径零 HTTP。另启动实际 Linux 托管 Worker，发送只有 hello + diagnose、无图片帧，得到 UnsupportedPlatform、退出清理；篡改私有帧与非协作原读/写/flush 测试核验原客户端拒绝、持有和清零行为。四个实际子进程 DLL 与测试目录 DLL 哈希逐一相等。

**Linux 真实失败路径不等于 Windows 成功能力验收。** Windows 实际包身份/已安装语言/原生 OCR 上限与质量、ComboBox/模态窗口/焦点、真实 WPF 测量/选择/阅读锚点、COM/Win32/截图/原生 Job/取消时限和 250/600ms 验收仍 NotRun。完整 P6-03、I/J、P0/P2/P4/P6 不标通过；P6-03 保持 InProgress/NotRun，P6-02 InProgress/Blocked，P6-04/05 不变。未跑全 Core.Tests/CMake。

固定 [JSON](../test-evidence/2026-10-16-ocr-installed-capabilities.json) 记录源清单、实际 TRX、失败、二进制和原件哈希。24 份新原件为 14 SDK log/TRX、9 份父版/失败源码与 1 preflight，精确路径 `-text` 保全；父累计 270 原回执、1858 既有证据与除明确变更之外的父文件逐字节核验。43 锁、8 workflow、8 模板、项目引用/依赖版本、Core/Provider 生产字节不改。

250/600ms、原 15 秒及全部来源/密码/完整性/Job/process/取消/清理门保持，生产离线复用 Off。每次 dotnet 激活前后及命令显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；证书存储/信任 Unknown，不读库/配置/trust/删除。Skia 许可与源码 403 阻塞不变，不 restore/install/load/execute 或绕拒绝。Actions 不查不调不绕拒绝，run 数 Unknown；无真实模型/费用/凭据/安全权限/新协议接受、Windows/CMake 探针、新 CI、main/强推/部署。
