# 八角色实际托管发布

本片接续 P7 的真实包输入链，将此前一次性工作区命令变成仓库内可重复的 `python3 -B scripts/publish-windows-managed.py artifacts/<fresh-directory>`。固定七产品角色和 Launcher，使用既有隔离 SDK 10.0.402、10.0.13 runtime、原锁文件及正常官方 NuGet audit；每个 role 的 restore/publish 共用新目录内的 `--artifacts-path`。不执行产品程序，不生成安装 catalog 或 source pins。

产品源码、嵌入 assets、licenses、锁文件和构建属性必须与原 HEAD 一致；实际清单在前后逐 hash 比较。复用 helper 字节亦在清单中，发布器本身单独保存原字节/hash。三个替代构建条件 TextAlpha/IsolatedSessionTest/WindowsCiProbe 显式关闭。所有原命令登记 argv、PID、原组、退出码及结算状态，原 leader join 后只观察自己创建的组；活子进程未退出不会继续，僵尸明确记录为已退出但未由系统回收。命令总截止 600 秒，组退出观察另有最多 5 秒；核 hash 时间另记，不把它们说成绝对十分钟总时长。自有输出在途监测 6GiB，至少预留额外 1GiB；失败与 UNKNOWN 保留原目录。

每个实际 publish 目录保留全部文件及 SHA。runtime 根据实际 includedFrameworks 选择 Core 或 Core+Desktop；固定归档 SHA512 后复用原 RuntimeList 映射和 WindowsBase 冲突规则。WPF 发布额外按官方 RuntimeList 的显式 WPF Profile 选择：SDK 10.0.402 的 [ResolveRuntimePackAssets](https://raw.githubusercontent.com/dotnet/sdk/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/ResolveRuntimePackAssets.cs) 对非空 profile 同样排除没有 Profile 的项目。原 helper 的 409 文件清单及旧报告不改；本发布器对真实 WPF publish 核 395 项，Core 核 187 个 runtime 成员，再从同一固定包附两份许可，按原 Bootstrap 的189项合同核验，每个被选成员仍逐 hash 验证。Launcher 另核原 189 成员、完整 deps 与 C# SelfContainedRuntime 全 JSON；拒重复字段和 bool/number 混同。PDB 等附加发布文件保留，完整 publish 目录不称为已封存的194文件B。

runtime 归档签名并未在本片重验：仅核与先前已批准、验签的固定归档相同，且显式沿原签名环境门拒 offline/禁验签配置。项目 locked restore 的正常 audit 不是本轮 runtime 包的新签名或漏洞验收。产物状态始终是未封存托管输入；真实 native/G、origin、issuer、Windows 执行和生产资格仍未由本片证明。

## 失败原件与修复

r01 在首次 restore 遇 NU1004：全局 `-r win-x64` 传播到原本无 RID 的引用类库。随后原组检查触发 UNKNOWN，但旧报告误写 Returned 且漏当前命令，是独审实际捕获的 P2。原件保留，不能当作成功或清理通过。修复去掉全局 RID，由原可执行项目自己的 win-x64 声明决定；原锁不改。新增 `-m:1`、禁节点复用，异常也保留原命令和真实结算状态。只读后验观察 r01 剩余 dotnet 为已退出 zombie；没有按猜测 PID 干预旧进程。

r02 App restore/publish 均 exit 0、原组为空；旧宽映射因 WindowsFormsIntegration.dll 及13份资源无 Profile、实际 SDK 不发布而拒绝。按上述官方源码修正筛选后，对同一 r02 App 输出只读复核得到395 runtime/417文件；未补造文件，也未改 r02 失败记录。最终新管线及局部用例的结果见[机器报告](../test-evidence/2026-10-25-managed-publish.json)。二进制、完整清单及日志只在原环境 artifacts 中保留，不随仓库传输。

## 四 native 的补齐条件

原源码仍为 src/KuaiYi.CaptureNative、OcrNative、OleNativeBridge、ShellExtension 及其原 CMake/两项 CTest。与历史构建 `147ab977b3725b15d35e55b0efb01110edb9d297` 比较无变化。历史四 DLL 已有独立回收记录，不能说从未构建；本新环境没有该 decoded 原件，旧环境不读取，旧拒绝的下载/API路径不重试。

本轮只读仓库 metadata 为 public。GitHub 官方说明[公开仓库标准托管 runner 免费](https://docs.github.com/en/billing/concepts/product-billing/github-actions)，[windows-2022 工具清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md)列 CMake 3.31.6、VS2022、Windows SDK 10.0.26100.0；网页不证明新 runner 实际配置。既有 workflow 已改作 overlay，旧 native source gate 固定旧 parent，均不可直接重跑。

拟申请仅一次新的标准 windows-2022 原生构建窗口：固定审过的源码 SHA，contents:read、原固定 checkout SHA/不持久化凭据；只用预装 VS2022 v143 14.44 工具集、SDK 10.0.26100.0、匹配 CMake/CTest，逐项记录实际完整版本，缺件即停，不安装或接受新条款。按四原 CMake 的 x64 Release、/W4 /WX、静态 CRT、parallel 2 构建四 DLL，仅运行 GdiReadOrderingContract 与 CommandPolicy 各1次。job15/step14/内部12分钟、attempt1、无matrix/retry/cache/artifact上传或费用；仅按原有界普通 job 日志保留结果。本轮未创建或触发该窗口。

具体执行前需审定新的 native-only 库存入口，避免复用 Get-CiEnvironment 所带的桌面/注册表探测；不启动产品、操作真实 registry、删除、Shell、身份包、证书、设置密钥、登录启动或用户电脑。该新 Windows 窗口的确认来自本轮委派要求；原 SDK 安装批准不能代替它。原产品 pins 保持 Unavailable，普通启动 issuer 仍需原模块/持有文件绑定和两跳原进程生命周期验证。

## 本轮实际结果

r03 八角色的16条 locked restore/publish 均 exit 0，原组全部为空；整体原报告仍为 Fail，最后因尚未复制两份许可文本而停止。修复打包步骤后不再编译，复制 r03 八份原输出到全新 packaged 目录，调用最终代码添加两份固定包原许可，再核全部角色、Launcher原合同与8份Core。8份Core均为 `863dee468b5d81695b83284d97036186bc1dca0d3015ab14a17181ea310fff33`。449份产品输入前后相同，原始publish目录前后逐hash不变。最终11项局部测试全过；这是11项，不累加早期7/8/9/10项运行。最终脚本中的完整CLI并未在许可修复后重跑，实际验证为已成功的16命令加最终打包函数集成；不能冒称最终提交被整体编译。

原件位于 `artifacts/2026-10-25-managed-publish-r01`、`r02`、`r03`、`artifacts/2026-10-25-managed-publish-packaged` 和 `artifacts/2026-10-25-managed-publish-checks`。包后8目录共2665文件；Launcher 196文件中包含原189+5项以及两个PDB附加件。没有删除失败原件或将二进制提交/上传。命令可重复执行，本片未证明跨隔离目录 bitwise 可复现；r02/r03 Core字节不同如实保留。本片不扩全Core/真实Windows测试。
