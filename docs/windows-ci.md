# Hosted Windows 合同验证

这套工作流只证明被记录的 runner 中的原生编译、DLL ABI 加载和公开 fixture 的 HWND 合同。GitHub 标准 x64 Windows runner 是 Server/管理员/UAC 禁用环境；它不替代 SPEC 的 Windows 11 x64 普通用户、跨应用、混合 DPI、OCR 身份、现代菜单或安装卸载验收。P0/E06 与 106 整体验收状态保持原状。

## 入口与触发

`.github/workflows/windows-contracts.yml` 仅在 `dev/p0-foundations` 的该工作流文件本身被推送修改时触发。没有 main、PR、定时、部署或发布入口。第一次推送须在监督方独审放行之后进行；改其他代码不会自动重新执行。`workflow_dispatch` 要求默认分支中已有工作流，本仓库 main 尚无工作流且禁止修改，故未采用该入口。

使用标准 `windows-2025`、15 分钟超时、仓库 `contents: read`，checkout 精确固定在 `d23441a48e516b6c34aea4fa41551a30e30af803`（官方 v6 标签于 2026-10-12 核对，Node 24），`persist-credentials: false`、`set-safe-directory: false`。没有真实 API Key、模型调用、付费 runner、artifact 上传、账户或 UAC/权限/证书配置。

## 实际脚本行为

`scripts/run-windows-ci.ps1`：

1. 核对规格原字节及追踪项；记录构建 SHA/dirty、OS 真实 ProductType/build、会话、管理员/令牌完整性、EnableLUA、窗口站/输入桌面、显示器/工作区/DPI、DWM、Shell、镜像及工具版本。探测线程自身 DPI awareness 也记录，不能把其 GetDpiForMonitor 结果当作混合 DPI 验收。
2. 只使用预装 VS 2022 / MSVC 14.44 系列、CMake >=3.28、Windows SDK **10.0.26100.0**；选择唯一 VS 实例，读取已安装工具链的精确补丁，显式传入 CMake `-G/-A/-T/CMAKE_GENERATOR_INSTANCE/CMAKE_SYSTEM_VERSION`。本次精确工具链会写日志，完整 P0 工具链长期锁仍待首次实测后审阅；没有宣称该任务已经通过。
3. 编译四个现有 C++ DLL x64 Release，不改变 `/W4 /WX`。检查4个 PE32+ x64 DLL、真正加载4个 DLL、解析8个导出并释放；不调用 OCR/捕获/COM 业务，不注册 Shell。真正执行现有 `CommandPolicy` 和 `GdiReadOrderingContract` 两个 CTest，验证其 JUnit 的实际执行与失败状态。
4. 图形会话、Unlocked Default input desktop、显示器、DWM、生产托盘所需 Shell 都存在，且预装 SDK **10.0.401** / WindowsDesktop runtime **10.0.12** 满足时，才 locked restore/build 现有 GrowthHarness。NuGet 只还原既有锁定依赖；不安装 SDK、WinAppCLI 或系统组件，不接受新协议。
5. 新增测试工具参数 `--ci-actual-overlay <csv>`。运行真实 ShellWindow，全部 selection/drop/hotkey/HTTP 接线关闭，仅向原呈现接收器注入公开 synthetic 内容。20 秒内部时限，完成后观察1200ms再正常关闭；外部35秒超时只终止自己启动的进程并等5秒确认退出。九个检查为计划/实际锚点与工作区包含、HWND读回匹配、前台不变、按钮锚点匹配、锚点稳定、实际生长；至少10帧、不丢帧、收到完整流、无错误才成功。

生产 Alpha 守卫完全保留；独立测试工具允许 Server/admin fixture 合同，不把它冒充 Alpha 放行。守卫当前以 build 判断 Windows 11，不能区别 Server，本脚本独立记录真实 ProductType。现有 CTest 是策略/顺序合同，不是 Windows GDI 像素或 Explorer 菜单实测。

## 退出码与证据

- `0`：限定合同成功，或 `-ProbeOnly` 仅完成事实探测。Native Pass + Overlay Blocked 仍会清楚打印两个状态、原因和零个桌面检查；**Windows11Acceptance 始终 NotRun**。
- `2`：原生平台/预装工具或完整环境探测不可用，两个测试状态 Blocked。
- `3`：实际构建、加载、CTest、已启动的 HWND 合同失败；保留阶段失败、部分实际计数和错误。公开探针自身失败3/超时4由监督脚本转为合同失败3。

每次仅生成唯一 `artifacts/windows-ci/<guid>` 目录，未触碰历史包。环境 JSON、DLL SHA/导出、CTest JUnit、公开帧 CSV、最终 JSON 全部写入 job log；`GITHUB_STEP_SUMMARY` 写明确计数与验收界限。不上传 artifact，不打包安装器或 Alpha。`-ProbeOnly` 不启动测试、构建或下载。

本地验证：43项 PowerShell/公开合成头与回执合同、2个实际入口子进程（Linux Blocked=2、ProbeOnly=0）、49项既有 overlay 纯逻辑测试、9工程 GrowthHarness Release 交叉构建零警告/错误。首次测试辅助函数参数与循环变量同名导致用例文件路径错误，原始失败日志保留；修正辅助函数命名后通过。第一次 pwsh 缺少可写 XDG 目录在进入测试前退出134，之后使用既有 workspace XDG 配置；该启动失败未计为测试执行。Windows DLL/桌面/远端 CI 尚未运行。

官方核对：[runner 镜像清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2025-Readme.md)、[runner 权限与公开仓库标准 runner 免费说明](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)、[微软 GUI CI 说明](https://learn.microsoft.com/en-us/windows/apps/develop/ai-assisted/testing#run-tests-in-ci)、[workflow_dispatch 默认分支要求](https://docs.github.com/actions/managing-workflow-runs/manually-running-a-workflow)。
