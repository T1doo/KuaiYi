# 开发进度

日期：2026-10-11。分支：`dev/p0-foundations`；初始完整 HEAD：`51d077481cf6303602fcf5d2718213da9cf05178`。

P0 整体：**InProgress，真实 Windows 退出门槛 Blocked**。后续整阶段均未验收。

原始产品书与 SPEC 均为 1826 行、146706 字节，SHA256 `ea3aa6529405bcc703986988fd089f476ee8f991d867e2220b3b20c439126166`；保留原文不覆盖，176 个追踪条目完整。

| 任务 | 当前实际工作 | 实现状态 | 验证状态 |
|---|---|---|---|
| P0-01 | 开发分支、SPEC/AGENTS、精确 SDK、集中最小测试依赖、锁文件、检测/构建/测试脚本 | InProgress | Linux 部分 Pass；完整 Windows 工具链 Blocked |
| P0-02 | 实际 UIA MTA Broker、限定窗口搜索、父进程硬终止、公开文件选区比对探针 | InProgress | 交叉编译 Pass；实际应用选区运行 Blocked |
| P0-03 | 实际非激活/主动阅读/拖动 HWND 生长探针；OLE 未实现 | InProgress | 交叉编译 Pass；Windows HWND/OLE 实测 Blocked |
| P0-04 | 强类型布局、合法方向/平衡、帧级边界、单调高度跟随、实际 HWND 帧采样代码 | InProgress | 10000 组纯逻辑 Pass；混合 DPI HWND 测量 Blocked |
| P0-05 | 包身份、OCR、现代菜单与安装态验证合同 | NotStarted | Windows、签名/信任与安装环境 Blocked |
| P0-06 | 双协议真实本机 HTTP/SSE、分片/错误/断流/取消、IPC/Native Messaging、独立 stdio echo、图像重组 | InProgress | 离线协议部分 Pass；截图/浏览器实际传图未运行 |

本轮还完成独立的启停/暂停/模式/请求身份门控、精确 origin 授权、七份原文提示词资源；属于可独立实现的基础合同，不表示 P1/P4 完整阶段通过。

## 真实运行结果

- `dotnet test KuaiYi.sln --no-restore` 首次编译暴露 IPC int→uint 错误，修复后 Core 22/22 通过。
- `pwsh -File scripts/bootstrap.ps1` 已运行；SDK/Pwsh 确认，WindowsDesktop=Blocked。
- `pwsh -File scripts/test.ps1 -Suite Offline`：Release 构建 0 警告/0 错误；Core **22/22**、Provider **35/35**，总 **57** 项，0 失败、0 跳过。
- 布局单项覆盖固定种子 10000 个合法拓扑/锚点/缩放及 210000 帧采样；这不是 10000 个独立 xUnit 用例，也不是实际 HWND/DPI 验收。
- 协议测试通过真实 TCP/ASP.NET 进程执行；独立 SSE 测试强制 1/2/3/7/4096 字节分片，覆盖中文、代理对、CR/LF/CRLF、BOM、多行 data 与未完整 EOF。
- 首次 Provider 测试 34 通过/1 失败：`explain@1` 后缀匹配误撞 `image-explain@1`。已改为精确逻辑资源名并完整重跑通过，没有删除或弱化测试。

## Windows 探针与第二次协议检查

- 首批完整提交 `60e894a3b3b6f5989fd89626098ce9aa6459e600` 已推送 `origin/dev/p0-foundations`，main 保持启动基线。
- 最新 `test.ps1 -Suite Offline`：Core **45/45**、Provider **43/43**，共 **88**，0 失败/0 跳过。新增独立 stdio 进程、字节限界、部分帧、图像顺序/总量/摘要/截止、所有权清零，以及 Anthropic 开始/块/结束生命周期与思考隔离测试。
- `build.ps1 -Scope Managed` 与 `pack-probes.ps1` 实际运行：11 项托管工程 Release 0 警告/0 错误；三个 Windows 探针发布为框架依赖 PE32+ x64。ZIP 为 `artifacts/KuaiYi-P0-probes-win-x64.zip`，摘要见本轮 evidence；无签名或安装器。
- 初次 Broker 编译发现 WPF 隐式 using 不含 System.IO，补明确引用后通过。核查发现默认交叉构建 apphost 为 ELF，改为精确 win-x64 并在打包脚本校验 PE 签名/架构。
- 代码复核修复同 PID 多窗口候选、JSON 清零前数据所有权、拖动误触发点击、帧中磁盘写入。实际窗口读回与计划矩形分别记录；未执行的图形行为保持 NotRun。
- WinRT SDK 投影包对当前 Win32/WPF 探针不必要，其许可证元数据要求接受；已从有效依赖及输出移除，没有执行显式接受新协议。有效引用包版本/许可证与 NuGet 审计见 dependencies 与 test-evidence。原生 SDK/MSVC 仍 Blocked。

运行产物：`artifacts/test-results/offline/KuaiYi.Core.Tests.trx`、`KuaiYi.Provider.Tests.trx`；持久化的无隐私运行证据见 `docs/test-evidence/`。

## 当前边界与继续目标

当前没有可安装的完整主产品、原生 Shell 或浏览器扩展；没有 DPAPI、OCR、真实跨应用与动画帧时间通过证据。真实 API 和质量人工评审未运行，未配置真实凭据。

本轮形成可交付审阅的 Windows 探针和离线协议切片。探针运行说明、实际限制和下一步真实矩阵见 windows-probes.md。P0 尚缺真实 UIA/焦点/DPI/OLE、截图、身份/OCR/菜单及原生工具链门槛；后续生产阶段不越过这些退出门槛。还需生产 coordinator/配置/DPAPI、broker ACL、授权复制、OLE、图片 Worker、浏览器/Explorer 和安装卸载链路。未实现项保持独立缺口，不能以探针 ZIP 代替用户要求的完整产品。
