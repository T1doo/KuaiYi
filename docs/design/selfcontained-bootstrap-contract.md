# 自包含稳定 Bootstrap 的内部合同

接续c8e65160b04ca0847425f2ee0fb5c04dcdbd1a44，不新增观察器/恢复内核/安装入口。本轮首次对现有Launcher执行真实no-restore、自包含win-x64/10.0.13、多文件不裁剪publish，实际NETSDK1112：Microsoft.NETCore.App.Runtime.win-x64未下载到SDK可解析位置。原始归档在私有输入目录，但安装进cache前所需正常在线验签资格未满足，未注入归档、切离线或重复七次同门。没有实际自包含publish产物。

## 两个不交叠的资格集合

保留FrameworkDependentDevelopment精确5文件、单文件32MiB/总64MiB与原runtimeconfig/deps完整JSON合同。新增SelfContainedWindowsX64_10_0_13：同5个产品文件+原获准Core runtime RuntimeList187文件+LICENSE/THIRD-PARTY-NOTICES两文件，精确194文件，另catalog；单文件仍32MiB，总128MiB（runtime成员实际80,374,232B，旧64MiB不能容纳）。不带WPF/Skia或外部依赖；PDB不是该固定运行闭包成员。

嵌入Core的固定runtime合同来自已获准10.0.13完整SHA512归档，每条size/SHA256、许可条目及RuntimeList版本元数据保留；它是内部资格要求，不是G/B source pin或本轮验签收据。源catalog每项已经由原InstallationPackage.ReadSource校验实际字节，SC再对runtime条目比固定合同，重新计算source SHA不能接纳不同runtime。Managed Core/Launcher源revision、Core与候选G字节、apphost绑定等原检查不变。

runtimeconfig精确tfm=net10.0、includedFrameworks仅Core10.0.13和两个原安全configProperties；framework(s)、探测路径、未知/重复字段均拒绝。deps按两产品project+唯一runtimepack的固定完整结构比较，RuntimeList中的172 managed+15 native资产、版本、RID/名称/路径全闭合，不接受额外runtimeTargets/库/资产或大小写替换。

SDK结构依据为[10.0.402 ResolveRuntimePackAssets](https://raw.githubusercontent.com/dotnet/sdk/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/ResolveRuntimePackAssets.cs)与[DependencyContextBuilder](https://raw.githubusercontent.com/dotnet/sdk/v10.0.402/src/Tasks/Microsoft.NET.Build.Tasks/DependencyContextBuilder.cs)。当前模板是据固定SDK源码和固定包元数据制定的失败关闭合同，尚未与本轮真实publish结果比较，不能因此认定可运行；如真实SDK输出不同，先保原件审清差异，不能运行时放宽。

## 原部署链绑定

ModeOf只从精确已验证文件集合推导，错误集合不降级。B候选携带实际Mode，Prepared和独立B事务写对应receipt；RequireReceipt验证已知Mode后重新读原包并比较actualMode，开发B不能改标签获得SC资格。Entry按该已知模式的静态完整集合打开原文件：开发6个、SC195个含catalog；原admission/原文件所有权/双错误保全和关闭结算保持。新增ReadDocument/hash均借原PreparationIO，未把局部using资源替代ledger，原容量预检保持。

仅内部组件测试能在当前环境推进：固定条目重pin、配置/资产重定向、实际开发receipt错标反例。不会手工构造一个SC目录充当publish正例。完整SC Read成功、真实自包含Launcher启动、原195句柄在Windows上的行为均NotRun。

## 具体最小前置

已具备.NET SDK10.0.402、win-x64 apphost10.0.13、WPF ref10.0.13、四获准Skia4.153.1包与两个runtime原始归档。缺可消费的Core/Desktop runtime10.0.13 pack及本轮正常在线签名资格；七角色发布必须沿原锁/官方源恢复且通过既有审计/验签门，不切offline/忽略警告或改信任/网络。

四native当前全缺真实输出。Capture需要MSVC x64/C++20、Windows SDK C++/WinRT及windowsapp/ole32/d3d11/dxgi/user32/gdi32；OCR需要同C++/WinRT及windowsapp/ole32；OleBridge需要MSVC/SDK ole32/uuid；ShellExtension另需RC、ole32/shell32/shlwapi。四项目均要求CMake>=3.28、/W4 /WX /EHsc、Release静态CRT。既有编译基线为VS2022/v14314.44及原生SDK10.0.26100.0；当前Linux无cl/CMake/RC/该SDK，不是缺.NET SDK，也不是要用户电脑。未安装新工具、接受协议、改系统或访问已拒runner接口。

真实扩展origin、完整G/native包、实际B publish、当前包验签以及冻结G/B审定仍必须齐备，之后才构建绑定pins的独立Installer；生产Unavailable不变。真实隔离Windows首次安装、升级失败恢复、新旧READY/退出、身份及250/600ms仍另验。
