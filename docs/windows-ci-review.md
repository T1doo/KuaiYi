# 首次 Windows CI 执行前独审

基线 `2c4531043be2b704a1f95517a3907b2e0a53242f`，分支 `dev/p0-foundations`；main 未改。此切片是已授权的最小 P0 平台验证，不推进 P5/P6，不放宽生产 Alpha 守卫。

## 可审阅改动

- `.github/workflows/windows-contracts.yml`：仅指定开发分支 + 该工作流文件路径的 push；只读权限、固定官方 checkout SHA、无凭据持久化/全局 safe.directory 配置、标准 windows-2025、15分钟；不上传 artifact。
- `tools/ci/WindowsFacts.cs` + `scripts/windows-ci-common.ps1`：只读真实 OS/会话/桌面/令牌完整性/UAC/预装工具，缺条件阻塞；PE/JUnit/回执校验。Server 与普通 Win11 明确区分，线程 DPI 上下文也记录。
- `scripts/run-windows-ci.ps1`：四既有原生 DLL x64 Release、2既有 CTest、4 PE/4真实加载/8导出；独立计数。图形环境可用才 locked build/public HWND 合同，子进程有界且只终止自身。错误不算通过，生成唯一临时证据目录，不重打历史包。
- `tools/KuaiYi.GrowthHarness`：仅测试工具新增 `--ci-actual-overlay`；真实 ShellWindow，关闭真实 selection/drop/hotkey/HTTP 接线，公开 synthetic 内容，20秒内部时限/完成后1200ms，真实逐帧九项约束，少帧/丢帧/未完成/错误拒绝。生产 App/Alpha/原生源码未改。
- `tests/scripts/windows-ci.test.ps1`：43项编译/解析/合成PE、回执、能力合同 + Linux 下2个实际入口子进程。测试 fixture 明确不是生产 DLL 或桌面证据。

## 已执行与未执行

已执行：45/45脚本合同（含2个实际 pwsh 入口子进程）、49/49既有 overlay 纯逻辑测试；GrowthHarness 所需9工程 locked restore/Release交叉构建零警告/错误；YAML结构核查与 git diff --check。没有重跑未变化的1836/63全量回归，也未把历史结果计为新测试。

首次辅助用例参数名与循环变量同名导致 ParseFile 路径错误，失败原日志 `2026-10-12-windows-ci-script-first.log` 保留；改辅助用例参数名后41通过，新增严格执行/布尔检查后43通过，加入两个实际入口后45通过。首次 pwsh 无 XDG 配置在测试前退出134，使用既有 workspace XDG配置恢复，未把它计作测试失败或Windows证据。

尚未执行：所有四 DLL 的 Windows 编译/加载、两个 Windows CTest、公开 HWND 合同、远端 Actions。CLI 在Linux的真实返回为正式执行2/Blocked、ProbeOnly0/NotRun，所有Windows计数0。

**独审后才允许正常推送触发一次。** 此提交只存在于本云仓库开发分支，本轮尚无 CI run URL。放行后推送该精确 commit，核对 run head_sha 和事件/分支，收集 job log/summary 的真实版本、计数、SHA和失败/阻塞；Actions 明确拒绝即停止，不更改权限或绕过认证。

无安装组件/显式协议接受、无真实 API Key/模型/付费、无账户/UAC/证书/安全策略变更、无main修改/部署/发布。即使CI全部合同通过，Windows11Acceptance仍NotRun，P0/E06及106整体门槛不变。

证据入口：`docs/test-evidence/2026-10-12-windows-ci-script-ready.log`、`2026-10-12-windows-ci-overlay-tests.log`、`2026-10-12-windows-ci-growth-final-build.log`、`2026-10-12-windows-ci-host-summary.json`。设计及复现说明见 `docs/windows-ci.md`。
