# 文字 Alpha 的自包含构建和有界本地运行

本包是 P0/P4 的托管文字 Alpha 准备物，Windows 实际运行/取词/焦点/动效/混合 DPI/整体验收仍未执行。它没有安装器、快译原生 Capture/OCR/Shell/OLE DLL、身份包或浏览器注册，不是全部功能已完成的产品。历史审查包保持原字节。

构建：在原仓库用锁定 .NET SDK 10.0.401 执行 `pwsh -File scripts/stage-text-alpha.ps1`。脚本按固定 win-x64/.NET10.0.12 自包含、多文件、不裁剪发布；每个宿主先独立 publish，再只在完全相同摘要时共用 runtime/依赖，冲突即失败、不覆盖。Alpha 的 RID 图使用 `packaging/text-alpha-locks` 中独立的锁，不覆盖普通构建原锁；只有维护者首次建锁/明确升级时使用 `-InitializeAlphaLocks`，审核后再次实际 locked-mode 发布。输出到唯一 GUID 目录和唯一 ZIP，包含每文件摘要、宿主归属和实际 commit/dirty 标记。自包含含 .NET/Windows Desktop runtime，不能代表 Windows 原生功能已验。

包内 `app` 是实际主程序、UIA Broker 和授权 ClipboardHost；`diagnostics` 是实际 UIA/生产浮窗探针；`test-provider` 是单独隔离的测试 HTTP 服务，正式程序依赖图不引用它。不得把测试 Provider 的固定结果当成真实模型质量证据。

解压后先只校验，不运行任何桌面进程：

0440757发布大包中的旧launcher存在退出码不传播缺陷。使用最新仓库或独立退出码补正目录中的脚本，`-BundleRoot` 指向旧包目录；不要覆盖旧包/manifest。本补正见[p4-alpha-exit-review.md](p4-alpha-exit-review.md)。

```powershell
pwsh -NoProfile -File ./run-text-alpha.ps1 -BundleRoot .
```

只有用户明确授权该 Windows 11 x64 测试会话后，才执行以下命令。需要普通用户、已解锁 Default 交互桌面、PowerShell7，以及受控测试应用。包内 runtime 无需另外安装 .NET；源码构建仍需锁定 SDK。已有快译进程时拒绝运行，不关闭或接管现有实例，不信任证书、不注册菜单/扩展、不读真实密钥。

```powershell
# 实际生产取词→本地真实HTTP协议→浮窗；初始仍Off，用户明确启用后只选择公开fixture。
pwsh -NoProfile -File ./run-text-alpha.ps1 -BundleRoot . -Mode TextAlpha -Execute -DurationSeconds 120
# 实际UIA探针：五秒内在受控应用选中公开fixture；Core矩阵仍须逐视图20次。
pwsh -NoProfile -File ./run-text-alpha.ps1 -BundleRoot . -Mode SelectionProbe -Execute -DurationSeconds 30
# 实际生产浮窗的公开文本生长探针：没有取词/模型，不能代替上一链路。
pwsh -NoProfile -File ./run-text-alpha.ps1 -BundleRoot . -Mode Overlay -Execute -DurationSeconds 120
```

TextAlpha 仅启动自己的回环 HTTP fixture 与实际 App 的既有 `--translation-test-provider` 入口；原不可变本地授权、无密钥、TEST模型、Off/epoch/来源/阶段/队列门控保持。只在受控应用打开 `selection-public.txt` 并选择全文；自行执行启用、收起、回看、停止和重新生成。通过设置选择另一协议时仍须保留同一回环地址、None认证和固定测试模型，否则已有安全边界会拒绝。不得改为真实服务或粘贴真实 key。

有界时间结束会关闭/终止本次实际持有的进程树，永不按进程名杀其他实例。证据写到系统临时目录的新 GUID 子目录；正文/请求体不写报告，只比较公开fixture是否匹配并记计数。无选择请求记 NotRun，不假成功；截图/焦点/帧率/完整兼容矩阵保留人工待测。退出/比较异常必须看终端实际错误，`WholeAcceptance=NotRun` 不因某个子比较通过而改变。

仓库 `run-desktop-tests.ps1` 是同一受控入口，`test.ps1 -Suite Windows -BundleRoot <目录> -WindowsMode TextAlpha -ExecuteWindows -DesktopDurationSeconds 120` 也接此入口；没有明确执行参数或 Windows 桌面就失败退出，预检不算 Windows 测试。Development/Release 安装包、完整 Windows UIA/COM/剪贴板/热键自动化 suite、原生 SDK/C++工具链锁定与所有阶段门槛继续待补；本 Alpha 不绕过 P0—P4，也不进入 P5/P6。

退出合同：0表示包预检成功或有公开子比较的限定会话正常/有界结束（整体仍NotRun）；2表示没有有效测试证据NotRun；3表示原文/fixture比较失败；4表示UIA探针会话超时；真实子进程非零值5/6等原样传播，其他异常非零。`session.json`的Outcome.Kind区分PreflightOnly、NotRun、LimitedSessionEnded、BoundedSessionEnded、Failure。不要只看shell0推断平台/质量验收完成。
