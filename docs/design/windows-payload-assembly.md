# Windows payload 来源清单与封装边界

接续3b243b2，停止增加恢复内核。目标是完整真实七角色payload→逐文件可追溯输入→完整封存G/B→审定source pins→独立Installer；各步骤不能互相冒充。本片先修真实OCR闭包漏项并建立实际组件输入清单，缺件不生成G/B catalog摘要或改Unavailable。

## 当前合法输入与真实缺口

已有授权：SDK10.0.402云工作区私有安装、两份Microsoft.NETCore/WindowsDesktop.App.Runtime.win-x6410.0.13固定MIT归档、四份SkiaSharp4.153.1及其所附条款。当前只恢复两份已有明确授权的runtime原字节，沿既有runtime-pack-integrity完整归档SHA512，不选新版/新源/新SDK，不关闭在线验签。archive hash/CRC/许可通过不是签名/执行通过。

当前Linux环境无MSVC/原生WindowsSDK/CMake，四份产品native DLL均无真实产物；四份为CaptureNative/OleNativeBridge/ShellExtension/**OcrNative**。后者由Imaging实际三项DllImport加载，原NativeFiles漏项需加入，自动进入TrustedFiles/派生trust/InstalledRoleFiles.Required，独立预期47→48。反例重算并固定新source SHA，走真实Apply缺失/截断/x86拒绝；正例仅结构fixture验证信任闭包，非Windows可运行包。旧47项不能自动补写/领养或回退，需要重新从完整源物化。

实际角色self-contained publish尚无本轮产物，不能将普通build手改runtimeconfig当publish。现B仅FrameworkDependentDevelopment五文件，不提供自包含bootstrap保证；完整B模式和逐文件闭包未验，不暗改其既有精确合同。实际1—2个浏览器extension origin也未固定，测试aaaa ID不得取代。

## 有界逐文件输入协议

新增一个只读组件清单命令，固定本仓库managed build路径、已批准runtime/Skia归档和Git资源。每项包括目标相对路径、字节数、SHA256、来源类别、源文件/包member、版本、源码revision或包完整归档hash；普通构建目录的bin字节不能仅凭obj声称源码归属，故sourceRevision=null/version=Unverified，仅另列declaredBuildRevision/declaredVersion并固定revisionVerified=false；仅Git资源、观察到的普通构建目录、精确包归档允许。拒未知/Fixture来源、越界/特殊/链接路径、大小/数量超限、大小写冲突与来源内容改变。runtime复用既有RuntimeList合并规则，唯一WindowsBase同版本Core→Desktop覆盖明确记录，不泛化冲突。Skia仅真实net10.0托管资产与win-x64 native及已提交许可，不加入macOS/Linux库。

清单明确kind=UnsealedWindowsPayloadInputInventory、status=Blocked、sealed=false：409个真实runtime成员等可记录输入，但不是已publish/可运行目录。缺native/已验证managed源码归属/当前self-contained产物/有效签名证据/真实extension origin/B自包含合同时，GenerationSourceSha256、BootstrapSourceSha256均null，InstallerPinsWritten=false；不创建installer-payload、安装根、注册项，不把清单digest作为source pin。输出拒绝..且核真实父目录在artifacts、拒链接，CreateNew清单输出fresh文件，不覆盖未知结果，不安装工具/查询403runner或触碰用户桌面。

下一封装需在合法已有Windows工具链中对固定源码实际构建四native与七角色publish，冻结完整目录，逐文件复核原来源后才封存catalog和生成待独审pins。不能消费仅声称Pass的外来build收据当真实来源，也不能将neutral PE fixture写入当前Unavailable资源。独立Installer必须在payload封存之后构建，避免摘要循环。

## 验证和未验

先保OCR原门同测试红→绿；原独立48项合同逐一缺receipt/缺file/改byte错误码保全。组件清单用当前真实已批准包读取，Python单测覆盖路径/来源/冲突/变更与缺件不签发；只跑相关Core/生产消费者最小回归，不重复完整Core/Provider/Codec。

真实Windows首次安装、升级失败恢复、新/旧READY与实际退出、身份包/Explorer/WPF/medium/250600仍需合法隔离Windows，不以当前结构测试或输入清单签收。缺新工具许可仅列明确组件/官方来源/用途，不自动安装、接受或请求共享系统改变。
