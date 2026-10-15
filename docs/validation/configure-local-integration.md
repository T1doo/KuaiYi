# 四文件诊断的本地接入（Windows 集成未执行）

本批仅本地实现、离线注入测试及只读独审，不推送、不启动 Windows CI、不重跑 CMake 或四生命周期。新环境初始 HEAD 为 `51d077481cf6303602fcf5d2718213da9cf05178`；默认 Git 网络只读核实远端 fixture 分支为 `b62af2fbd150d5921a789ed0767533f161dac066`、原诊断分支为 `bf0330cd80abe79a201762f47a9db92a0135f3c7`，且后者是前者祖先。本地开发分支 `dev/configure-files-local-integration` 从 b62 建立，main 未修改。单一工作树、排他本地 writer 锁；root 为唯一集成者，独审不写文件或 refs。旧任务不恢复。

当前仓库只有根 AGENTS.md，没有可用的 `.agents/skills` 或其他 SKILL.md。personal-context 返回 unavailable，未当作取回历史。GitHub CI API 返回 Forbidden 后停止，不申请扩权或更换接口重试；下述证据来源明确分开。

## 既有证据与缺失原件

已读取仓库原方案、失败分析、固定回执、fixture 代码和归档结果。`37fb5d5d63c27239567cf084c9109cdedcf1f504` 的唯一 parent 是 bf033，b62 只增加 fixture 结果文档；归档 CI38674331460 的 11 检查 Pass 只证明独立 Windows owned-file adapter。公开合成版本 3.31.6 不是实际 CMake 版本。

用户本次交接指出 CI38669606027 为 NativeConfigure Timeout、PreRescueJobState NonEmpty、BudgetExpired true，救援后才确认清理；原始 job 文本/新回执没有随这个新环境保留，不能声称本轮重新获取或验证其字节。仓库内更早 CI38595908121 的失败记录另存且未修改。具体残留进程、阻塞 API、MSBuild 节点复用或其他 Windows 原因仍 unknown。

初查及最终检查均未发现任何原始 `CMakeVersion.local.log`、`NativeConfigure.local.log`、`native/CMakeCache.txt`、`native/CMakeFiles/CMakeConfigureLog.yaml`。未补造、恢复、从 fixture 复制或从旧描述回填。测试内容只在自有测试对象/目录中，不能代表这些缺失原件。

## 最小接入及结果隔离

新增独立 `ConfigureRootLease.cs`；原 `LifecycleOperation.cs`、两个已验证 Python adapter/parser、workflow、生产取词及生命周期实现逐字节保持原样。runner 在自建 output 目录创建、原 helper 加载后，所有操作之前尝试持有非继承根目录句柄，捕获 volume/index；因此任何采集都有 configure 前的连续 lease。根打开为 FILE_OPEN、只读属性/遍历、OBJ_DONT_REPARSE；共享 READ|WRITE 允许原输出创建，但不共享 DELETE。身份/普通目录检查失败仅禁用诊断；本地 NTFS 判定仍由原 adapter 在内容读取之前执行。未新增服务、ACL、token/提权、系统配置或网络权限。

NativeConfigure 的原 Run 返回，即原救援、root/Job 结算、flush、句柄关闭路径结束后，才调用诊断；先诊断，再沿原 GetReason 返回/抛出。CleanupConfirmed 必须为布尔 true；否则不启动 collector、不 OpenProcess、不 DuplicateHandle、不读文件，最后关闭 lease。原 Status/Exit/Reason/PreRescueJobState/BudgetExpired/RescueUsed/JobEmpty/CleanupConfirmed 不被诊断改写。

collector 通过原安全包另开一次有界诊断进程，保持其既有 30 秒成功预算和原救援观察，独立于原 configure 的 30 秒；不进入原 Operations 或 compiler allLogs，不把诊断成功算成原操作成功。父同步持有根 lease 直到 collector 返回且采集/解析结束。Python `-I -B` 显式导入受控脚本目录；源 PID 来自持 lease 的父进程本身，源句柄只用于 `DuplicateHandle(sourceProcess, sourceHandle, currentProcess, ...)`。OpenProcess 仅 PROCESS_DUP_HANDLE，复制后立即关闭进程句柄；仅返回到 Python 的目标句柄才能进入原 adapter 的同进程 duplicate，最终关闭目标句柄。此接口依赖可信 runner 调用来源，不是接受任意 PID 的公开采集服务；没有进程枚举。

四个固定文件各至多读取 65,536 字节，新 post-cleanup collector 总至多 262,144 字节；缺失为 Missing，不创建文件，截断清空全部事实。只保留现有有限版本/配置/生成/编译器/ABI/working/VS2022/try_compile 事实。Python 标准输出经 8KiB、固定字段、重复键、精确字符串/布尔类型、顺序和每文件事实约束复核并重建；不透传路径、命令、原文、异常或任意属性。独立 KUAIYI_CONFIGURE_DIAGNOSTIC 行不改变原 summary schema/退出码；模块不可用、复制/读取/JSON/关闭失败仅改变独立诊断状态。

旧 finally 的 compiler 扫描排除 CMakeVersion 和 NativeConfigure，避免再以 4MiB 入口整读同一文件；CompilerFaults 不再从 configure 日志获取。原 CMakeVersion 前置工具门仍保留首行读取，64/256KiB 是新增 post-cleanup collector 的界限，不涵盖这一既有前置读取。其他 build/managed 日志只在清理确认后进入旧 parser；原 runtime 的末尾读取也只在清理确认后允许。

官方依据：[DuplicateHandle 的源/目标进程语义](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-duplicatehandle)、[NtCreateFile 的 RootDirectory/FILE_OPEN/共享与同步语义](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntcreatefile)。未声称同步 API 有逐次硬墙钟上限；外层预算也不是清理证明。

## 本地测试与独审

实际最终输出和六文件 SHA256 见 [固定证据](../test-evidence/2026-10-15-configure-local-integration.txt)；完整测试输出仅保留在 ignored 私有 artifacts。新 Python bridge 14/14；真实 runner 函数 AST 只替换两个 Run 调用目标的注入合同 127/127；原 adapter 21/21、独立 fixture 离线合同 16/16、no-target 静态检查 9、public evidence 45、runner 72 均通过。未删除或弱化旧断言。SDK10.0.401 未安装，依赖实际 SDK setter 的原 105 合同本轮 NotRun，不拿既有结果冒充重跑。

覆盖失败→救援→清理确认→读取→关闭 lease，未清理零读取，缺 lease/模块零 collector，读取/关闭/私有输出异常保持原失败及成功/1/Other 退出值，四文件/限额/缺失/身份拒绝，重复键、额外字段、越界版本、敏感哨兵、枚举数组/对象/数字/null 拒绝，真实 `-I -B` 非 Windows 入口。C# 仅编译/布局反射与非法输入前置拒绝，无 Win32 调用。

独审指出并已补正：接收失败/源进程关闭失败时目标句柄不得重复关闭；枚举字段不能让单元素数组绕过字符串类型门。root 同时补齐隔离导入、诊断依赖失败隔离及旧 compiler 重读入口，未等同于 Windows 运行结果。

## 剩余边界

新 C# lease、跨进程 OpenProcess/DuplicateHandle、原 runner 的真实救援到读取顺序及退出/清理行为，仍需未来单独授权的 Windows 集成验证；既有 11 项 fixture 不覆盖这些新增部分。没有真实日志，本轮不能进一步定位 CMake 根因；禁节点复用仍是候选假设。原 runner ReviewedParent/depth/workflow 来源门保持既有值，本地提交不是可直接运行的新 CI 冻结；未来执行前还须审定准确交付身份与祖先深度，不能绕来源门。

250/600ms 与既有来源、密码、完整性、取消、清理门保持；ProductionReuse Off、ModelCalls0。P0/E06 Blocked；四生命周期、真实 UIA/OLE、匹配/性能、普通 Windows11、多屏 DPI、安装卸载仍无新增验收证据。未推送、无新 CI、无 main/强推/部署/费用/凭据/安全权限改变。
