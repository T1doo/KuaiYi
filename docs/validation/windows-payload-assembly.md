# Windows payload 输入与 OCR 原生闭包

接续入口证据3b243b2e14ba658b41eee15a7b1d37d44f8e50a1。本片目标是实际Windows封装；当前没有完整可运行payload，生产Installer source pins继续Unavailable，不以结构fixture或输入清单代替。

## 实际产品差异

Imaging的NativeOcr真实DllImport使用KuaiYi.OcrNative.dll，原InstallationPayload.NativeFiles却未要求此文件。将其加入唯一原生必需集合，继承原x64 PE/source SHA/派生trust/installed receipt校验；独立48项合同由47项增加OCR，其他断言不变。旧47项catalog/trust不符合新合同，不能补写领养或回退，需要从完整源重新物化。

新四例经实际Store.ApplyAsync：重算并固定新source SHA仍缺失/截断/x86 OCR必须拒绝；正例必须把真实输入hash派生进trust，随后改字节使InstalledRoleFiles和ReadActive拒绝。native/runtime PE仍是明确结构测试夹具，非可运行Windows产物。r02原门同完整testfile4F→r03修复4P，r03另含原receipt18P，合计22P；不同轮数不累加。

新增scripts/inspect-windows-payload.py只观察本地已授权归档、Git资源与普通build目录的有界输入，不创建安装根/封存包/注册/pins。输出固定Blocked、sealed=false、G/B source SHA均null；仅fresh artifacts真实父目录，拒..、链接、覆盖。归档固定原完整hash并在同不可变字节快照内读取成员；复用RuntimeList原冲突规则，原publish逐输出hash校验保留。

普通bin不能凭obj版本声明证明源码归属：行类别ObservedBuildDirectoryInput/ObservedBootstrapDirectoryInput，sourceRevision=null、version=Unverified，声明另列declaredBuildRevision/declaredVersion，revisionVerified=false；实际字节SHA如实记录，VerifiedManagedArtifactSourceRevision仍是缺项。原Git资源逐字节与冻结HEAD核对；归档成员固定来源包、版本、member与完整archive hash。

## 当前真实依赖条件

在工作区私有目录恢复了此前明确获准的两份10.0.13 runtime归档；原SHA512、ZIP CRC、nuspec MIT无新协议接受。四份SkiaSharp4.153.1既有授权保留，本片仅选net10.0托管和win-x64 native输入及许可。包归档完整性不等于本轮在线签名通过，更不是执行通过。没有新工具安装、CI、凭据/安全权限或真实模型调用。

缺四产品native真实产物：KuaiYi.CaptureNative.dll、KuaiYi.OleNativeBridge.dll、KuaiYi.ShellExtension.dll、KuaiYi.OcrNative.dll。当前Linux没有原生MSVC/Windows SDK/CMake。需合法已有隔离Windows工具链：MSVC VS2022/v143 x64/C++20、Windows原生SDK10.0.26100.0头库/C++WinRT/RC、CMake>=3.28；它们不同于已批准.NET SDK10.0.402。本轮未新下载或接受新许可。官方来源：[Visual Studio工具](https://visualstudio.microsoft.com/downloads/)、[Windows SDK](https://learn.microsoft.com/en-us/windows/apps/windows-sdk/)、[CMake](https://cmake.org/download/)。这些链接用于定位条件，不代表新安装授权。

另缺七角色真实自包含多文件publish/逐文件可验证build来源、本轮有效在线签名资格、实际1—2个extension origin；现Bootstrap仍精确五文件FrameworkDependentDevelopment合同，不能暗称自包含。冻结完整G/B目录并独审来源摘要后，才单独构建绑定pins的Installer。本片不改Unavailable，不使用测试aaaa origin或中性PE当发布输入。

## 验证范围与证据

原件路径artifacts/2026-10-22-windows-payload。候选Python新5方法、原runtime19方法分别通过；原runtime套件最终Windows context报告Blocked，非Windows执行Pass。完整命令、日志、TRX、源码与产物摘要在原件保留。冻结后的必要managed构建、相关Core和实际归档输入观察另补结果；不重复完整Core/旧586/266/204，不借记未执行项。

真实隔离Windows首次安装、升级失败恢复、新旧READY/实际退出、Explorer/OLE/WPF/普通medium身份、画质及250/600ms未验。PackageHold、主Off及原来源/密码/完整性/取消/清理/发送门保持。部署内核Committed不代表真实安装成功。
