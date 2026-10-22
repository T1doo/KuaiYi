# 统一部署 Before 快照与 AbortBeforePrepared

基于根侧限定ACK的a808310e0199df92ac10194c5bb25cd99c73ada0 / 5a69c4c8fe7a1faa98ede79b618ad017b6b764be，接续SPEC13.3/13.4、P7-04/05。仅内部existing-only快照与终态，不是完整统一事务、安装成功或Windows验收。

## 生产路径

Scope在同一sync锁仲裁snapshot启动与finish封闭，先登记snapshot owner并发布唯一原Task，再开放执行。finalizer始终join内层原snapshot，Observe包装只等待结果；取消和Abort不能遗弃原读取，Closing后拒绝新snapshot。原单pending settlement Task及CleanupConfirmed任务门保留。

固定读取current.json≤4KiB、bootstrap-store.json≤4KiB、stable-bootstrap.json≤8KiB、host-registration.json≤8KiB。原FileStream先入scope ledger后读，正文及关闭错分别收集；缺失、空文件、目录、超限、读取错误严格区分。只将明确叶FileNotFound且其父为已确认非reparse目录视为缺失；捕获与Closing缺失复核共用严格查询，不将File.Exists的false作证明。

NativeHost新增只读InstallationDeploymentRegistrySnapshot：先发布capture/verify原Task，保留同一IHostRegistrationStore，每读Chrome/Edge后检查原raw-key关闭；未结算不读下一槽。保留KeyExists/Value/ForeignContent三字段，区分不存在与空key。两槽及四文件是观察值，不是归属或注册授权，未知原bytes不会被快照变成可信安装内容。

全部原读取和关闭确认后才编码Before；实际JSON必须≤128KiB，含Ticket SHA、operation、四文件原bytes/base64及两物理槽。registry每值≤32768字符仍需核JSON转义后的实际容量。Before写入尝试先于CreateNew登记，foreign冲突、partial Write、Flush或Close错误均永久Unconfirmed，保留pending与原件，不删、不领养。编码超限或写前取消尚未尝试Before时，可在原资源全部结算后早Abort，但Capture仍抛原错，终态Errors非空，不是假成功。

完整Before才允许AbortBeforePrepared：终态绑定Before SHA，Prepared必须null。Closing复核exact Ticket/Before、四文件存在性/bytes及两槽，所有新读取也由原finalizer拥有并关闭；Terminal/归档保持唯一原Move点及Move后零清理。Move失败或收据丢失不重建pending、不重读或重放；原资源显式清理也不改写原Unconfirmed结果。

## 本片边界

尚未准备任何候选。本片没有candidate/prepare/publish API，current、B active、registry和Host receipt均不写；测试只观察并验证原状态不变。现有Package.CopyTo/Materialize/Bootstrap.Read的局部using与失败删partial写法尚未抽取为共享原IO，不能直接调用或加绕过门。下一片才设计共享原IO的G/B只准备工厂及Prepared，再独审三方前向与反向补偿。本片无Committed、历史查询/恢复、fixed locator或注册/安装入口。

## 实际候选原件（轮次不相加）

artifacts/2026-10-22-deployment-before保存command/log/TRX、完整失败XML、对应源码、输入/DLL前后摘要。

- r01 CS0414未使用状态字段导致编译拒绝，零测试；r04 CS0619/xUnit2014要求ThrowsAsync，零测试。未关闭分析器或把零用例算通过。
- r02 partial Before9字节错误归档反例1F；r03完整测试文件相同1P，partial门闭合。
- r05本fixture实际父路径故障反例1F：旧Exists误当缺失后实际读registry两槽，期望0；r06同完整测试文件1P。只移动本测试专属目录/创建已知中性文件并原样恢复，未改ACL/权限。仅Linux合成文件系统证据，不代签Windows此故障。
- r07 Before20+原scope25＝45P/0F/0skip；r08针对Closing原registry读取/关闭双错及原托管Flush阶段错误2P。Flush例不冒称磁盘故障。
- r09入场后同operation foreign Before导致误早Abort反例1F；r10完整测试文件相同1P。修为beforeAttempted先于Open，保留foreign原件及pending。

独立只读审查对源码、三个红绿和候选原件限定ACK；均未代替运行。产品冻结后的实际签收如下，不借记旧111/586或完整Core/Provider/Codec。

## 未验与依据

真实Windows/medium/浏览器/WPF/Explorer/OLE/OCR效果/画质/250/600ms仍NotRun。三native DLL及完整自包含runtime仍缺，框架依赖开发形态不代签可运行Windows包或P7。未新增依赖/许可/凭据/安全权限/费用/真实模型调用，未操作用户电脑。Off、发送门、来源/密码/完整性/取消/隔离及250/600预算不变。

Microsoft说明File.Exists遇查询错误也返回false：[File.Exists](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.exists?view=net-10.0)；严格查询异常见[File.GetAttributes](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.getattributes?view=net-10.0)。本片通过原调用结果与父目录确认收紧，不声称文件系统跨外部进程原子CAS或断电持久性。

## 固定树签收

产品`a909c20981dd88fb31ecb42bf943071dbfec32f1`从clean树执行r11 App/NativeHost/Launcher三次普通managed构建，均0W/E；r12同固定树一次48P/0F/0skip＝新Before23＋原scope25，两类48唯一显示名称。没有重复旧111/586或完整Core/Provider/Codec，轮次不相加。

r11/r12四份输入快照各904项，before=after且逐项匹配产品Git。12目标AssemblyInfo含完整产品SHA，9产品目标DLL从普通构建至测试签收保持一致；测试及两fixture本轮正常编译，Launcher/CoreTests/两fixture实际Core副本与主Core一致。原件保留版本源码/摘要，未包含DLL本体，不冒称独立二进制重hash。

362原src中361未变、仅Scope修改，新增NativeHost只读snapshot adapter；旧tests全字节不变，新增23例Before文件。锁、项目、workflow、packaging、扩展、scripts、sln未改。完整命令、TRX、失败原文、对应源码与scope-audit.json一起保全。

固定树与原件独审限定ACK。原件Library libfile_348b60f9b7e08191833b0e3df952c14c（v0，1,877,796B，206members/205manifest，SHA256 ed0943a022ef9d92840a68f0ce603a2fdd61c63c43e198bc4c2bfc2ee2d512f1）；本地Library身份已核。ZIP CRC、205摘要及8份Git源码已独立核验，无DLL本体。证据提交仅三报告，产品a909c20981dd88fb31ecb42bf943071dbfec32f1不变；完整统一事务与Windows仍未签。
