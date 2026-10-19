# Windows SDK402：实际许可与临时 CI 安装范围

父级从正常登录页面取得 [run39221162999/job117956532058](https://github.com/T1doo/KuaiYi/actions/runs/39221162999/job/117956532058#step:3:15)：源码 `1662f6526147a98e5b6d42e148d18294b5397e55` 的两份 runtime10.0.13 包均正常在线 verify Exit0、Signature Pass，`SignatureStatus=Pass_WindowsNuGetFrameworkOnline`。预装 SDK402 缺失使后续 managed/build/publish/真实 Windows App 验收 NotRun，Package Hold。该真实结果由父级桥接，不能与本地离线 fixture 混同；未伪造尚未收到的完整日志或 TRX。

“不安装 SDK”是上一单次预装验证窗口的范围，不是永久技术禁令。微软 [Windows 手动安装说明](https://learn.microsoft.com/en-us/dotnet/core/install/windows#install-manually) 支持 CI 使用 ZIP 解到自有目录并只设置当前会话环境；不要求 MSI、管理员、系统 PATH 或注册表改变。最新授权允许沿用已接受许可时采用此路线。

实际 Windows 归档许可却与此前明确获批的 Linux 归档不同：

| 对象 | 实际字节与结论 |
| --- | --- |
| Windows SDK10.0.402 x64 ZIP | 300,755,826 bytes；全 SHA512 精确匹配已提交的官方发布元数据 |
| Windows 根 LICENSE.txt | 9,519 bytes；Microsoft Software License Terms / Microsoft .NET Library；含使用即接受条款 |
| 已批准 Linux 根 LICENSE.txt | 1,116 bytes；MIT；不是上述 Windows 条款 |
| 两份已批准 Windows runtime nupkg 根许可 | 均 MIT，不能代替 Windows SDK 的不同根条款 |
| Windows ThirdPartyNotices.txt | 78,887 bytes；仅比 Linux 77,436 bytes 多 1,451 个 CR；规范化后全文相同 |

确切资产：[dotnet-sdk-10.0.402-win-x64.zip](https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.402/dotnet-sdk-10.0.402-win-x64.zip)。完整 SHA512：

```text
ae4fd528cb7e9f6af6aaf7de36715a69f761f183744f1bb3025d19f5a92447090ec561410e46266c1d10a63781ed6f3de771760bc76c876d87daabc367b898cd
```

根 LICENSE.txt SHA256 为 `7f6839a61ce892b79c6549e2dc5a81fdbd240a0b260f8881216b45b7fda8b45d`。官网 [.NET Library 条款页](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm) 可供参考，但文本并非归档字节原件。依据 [SDK README](https://github.com/dotnet/sdk/blob/main/README.md#license)，归档内 LICENSE.txt 与 ThirdPartyNotices.txt 才是权威；批准应针对本轮保存的确切原文。当前记录只找到 Linux MIT 的明确批准，未找到此不同 Windows 条款已获接受的证据。因此停止的是新条款下的安装／使用，状态为 Pending，用户并未拒绝。

只读 ZIP 元数据已确认 SDK10.0.402、宿主10.0.13、私有 XML10.0.13、Core/Desktop10.0.13；5,577 条目，程序展开量 807,617,264 bytes。只保存许可和 JSON 元数据，SDK 程序未解压、安装或执行。官方 ZIP 请求200、hash通过；此前本机 blob 发布元数据请求 Tunnel403 已停止，版本资料使用仓库已有官方原件，没有重试或换源取得被拒元数据，也没有查询 Actions。

最小补充批准仅为：接受上述固定 Windows SDK 归档的 Microsoft .NET Library 条款，允许在一次公开标准 `windows-2022` 临时 job 自有目录安装并用于本仓库构建／测试。无需重批已接受的 Linux SDK；不涵盖 Skia、发布、系统安全／凭据权限或费用。

批准后的具体方案已封存为 `proposed-approval-and-ci-scope.json`：一次有界源提交，固定官方 ZIP／全 hash／许可 hash／安全解包；原 `require_safe_sdk` 静态门先于任何 dotnet；明确 SDK 子进程路径和仅子进程环境；正常在线双包验签、locked restore、审计 all/low／warnings-as-errors、原全量 Core/Provider、默认／隔离 App 和 Worker 构建。保留15分钟 job／14分钟 step／attempt1，拒401降级、失败换源、离线撤销、信任改变和自动加跑；无部署、publish、真实 App 或模型。本轮没有改脚本或 workflow，也未投递新 CI，Hold 不解除。

许可原文、包 SHA、metadata、父级真实 CI 摘录、独审与最小批准范围见 [证据索引](../test-evidence/2026-10-19-session-windows-sdk-assessment.json)。历史图片间歇根因仍 Unknown，生产图片复用 Off，250/600ms／来源／密码／完整性／取消／清理门保持；真实 WPF／模型及完整 P6/P7 未验收。
