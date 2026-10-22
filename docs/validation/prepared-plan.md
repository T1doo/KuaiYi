# Prepared 有界封存与 AbortAfterPrepared

接续根侧限定ACK：B产品f23bdcf387cfacb7d9c187c8c27739f1f7a97eb0、证据99933aebc942e8e94644386bc40330a7a61ab796。对应SPEC13.3/13.4、P7-04/05及统一事务设计；仅计划封存/只读归属验证和AbortAfterPrepared，不含前向发布、逆序补偿或安装入口。

## 原 scope 与资格完成点

SealPrepared仅接受同scope仍存活的原G与原B，且B.Generation必须是该G原引用；原Before snapshot owner必须同时提供Prepared读取接口，不接调用方传入的verified、receipt或替换Host owner。原Prepared Task及同ledger Prepared channel在producer开放前登记；finalizer join同一原Task，CleanupConfirmed包含该任务。channel只读安装root，唯一写权限是本operation固定plan的CreateNew；copy/create-staging/Move API均明确拒绝。

全部读取关闭确认后，actual Prepared JSON≤128KiB，目标current≤4KiB、bootstrap owner≤4KiB、active与Host receipt各≤8KiB及原START encoded≤16KiB均预检；包含Prepared SHA的实际AbortAfterPrepared terminal≤16KiB也在Plan写前核。plan以原IO执行CreateNew/分块Write/Flush(true)/Close及exact读取；不删除partial。BeforeQualification hook后再次exactPlan原读/关闭成功，再于原inner Task中的scope锁CompletePrepared仲裁token/finishing/closing、shared Unknown和原资源清理；此处是资格完成点，Observer异步恢复不倒推改写该已知阶段。

写前明确OCE且尚未尝试Plan、所有原资源已结算时可AbortBeforePrepared，资格始终拒绝；foreign/原读取错误，或Plan一经尝试后Write/Flush/Close/exact/取消失败，永久Unknown并保留pending/原件/同owner。资格前取消胜出不产生有效Prepared。Complete先胜后才取消/Abort，即便调用方continuation尚未恢复，凭据live立即false，但原finalizer仍以Prepared SHA和AbortAfterPrepared结算已知完整计划，不谎报未准备。正文错与每个原关闭错均保全，不能丢原Task或由新handle证明原清理。

## 旧 Host 与 Bootstrap 是实际验证，不借观察值授权

原RegistrySnapshot新增独立的一次Prepared read Task；重读同一原store的Chrome/Edge两物理槽，与Before精确相等，每槽原raw资源关闭不明就拒下一槽，另外保留一次Closing verify。Core只把它作为实际观察，再亲自验证归属：无Host receipt时两槽必须无foreign且默认值为空；同manifest路径而无receipt不领养。有receipt时必须原canonical格式、正确schema/product、非空owner、精确Before旧current和manifest；旧current对应的完整v3/来源/角色/派生metadata与payload通过原shared IO复验。旧receipt与旧current不一致先拒绝，不扩旧代迁移。

HostOwnership DTO移到Core共享，NativeHost旧独立事务用alias，字段/顺序/JSON shape及原门保持。没有调用含旧using的ReadActive或IntegrationInstalledFiles.ReadAsync，也没有以Configured枚举、假processPath或pid证明授权。Current计划复用Store内部纯Parse/Encode及shared IO读取旧v3。旧bootstrap owner/root/data、canonical container、active receipt/实际旧bundle/package/Core/revision同样经shared IO验证，receipt.Generation必须匹配Before旧current；默认旧入口仍保留原行为。

G/B在原读取前及写Plan前分别完整复验，防止registry读取挂起期间目标被替换后仍凭历史资格签发。Scope仍重核exact Ticket/Before及四份原发布文件。Windows外部非合作写入不是跨文件/registry原子CAS；不把观察时点当持续不可变承诺，下一片前向仍必须逐步重核完整计划、原owner及合法前缀。

## 计划内容与零发布

Plan绑定operation、Ticket/Before SHA、原G源/派生SHA、B源SHA/revision、新current（previous保留原active）、B inactive→canonical精确映射、目标bootstrap owner/active、旧Host归属与新两槽/receipt。新owner/receipt只编码，canonical目标必须未被未知child占用；原owner不存在时canonical bundles不能已存在，不创建owner/容器。B目录仍在bootstrap-candidates，G仍是inactive generation。

无current、bootstrap-store、stable-bootstrap、Host receipt或registry写入，无bundle Move、进程启动、READY、OCR或发送。AbortAfterPrepared复核原发布状态及原registry，Closing新metadata读取仍由原finalizer经同ledger拥有/关闭；exact Plan被替换或原关闭未知则保留pending。terminal绑定Prepared SHA，最后failed归档Move之后无可失败清理，不提供重放、Committed或恢复入口。

## 实际候选原件（轮次不相加）

artifacts/2026-10-22-prepared-plan保留每轮完整command/log/TRX/失败XML、对应完整源码、输入和产物摘要。

- r01七普通managed构建0W/E，零测试。
- r02确定屏障反例1F：原registry第三次读取挂起后改G Core，只有前置验证仍签发Prepared，实际error=null。r03正常Launcher构建同步新Core副本；r04完整测试文件相同1P，修为写Plan前重新完整G/B验证，保留损坏原件和pending。
- r05专项26P/0F/0skip。
- r06正常Launcher构建0W/E，零测试；r07最终28P/0F/0skip。追加terminal写前实际容量、资格前Plan复验及shared Unknown门，并覆盖旧payload损坏与资格前foreignPlan。

28例包含初始/已有归属升级的只读完整计划与SHA、旧Host无receipt/foreign/stale/empty owner/noncanonical/损坏payload、旧bootstrap owner/receipt/container拒绝、partial/Flush/Close/foreign CreateNew、取消三胜序和交付后取消、原read/flush屏障、registry双错、旧bootstrap原句柄双错、Closing原新读及foreignPlan、channel最小写权限。所有反例均中性合成数据；真实registry读取策略仍未实测。

内部独立只读源码及定向原件限定ACK，无确定新增P1/P2，未代替固定树回归。

## 固定产品签收

产品完整SHA：`9c2256774d70c970b4e1cd24aec7db0f2a2f12af`。r08在该clean HEAD按原锁/SDK执行七个普通managed构建，全部0警告/0错误、零测试；r09在同一clean HEAD一次相关Core签收204P/0F/0skip，204个唯一显示名：Prepared28、B30、G28、Scope25、Before23、BootstrapDeployment35、HostRegistrationTransaction35。即新增28＋既有176，不称完整Core，不借记旧128/141或把不同轮次相加。

两轮911项输入前后相同且逐项匹配产品Git；17份AssemblyInfo均含完整产品SHA，14个产品DLL的正常构建与实际测试摘要一致，Launcher/Core.Tests/SettingsStoreFixture/BrowserHostFixture四份实际Core副本一致。366份旧src仅改6增1，其余逐字节未变；336份旧测试逐字节未变，仅新增Prepared测试。锁文件/项目/工作流/打包/扩展/脚本均未改。scope-audit.json及原始command/log/TRX/完整失败XML/输入产物摘要/对应Git源码保全；原件不含DLL本体，不能冒称独立重hash二进制。

固定树原件独立只读复审限定ACK，无确定新增P1/P2；审核未另跑测试或构建。原始ZIP已保存Library `libfile_44b5928d8e1c8191901877454a513f17`，v0，1,551,923B，191成员/190manifest，SHA256 `a32f7e9251fed889c8fb7862aef81d5b98ae36d355d9ff9436dfc231400618d2`；本地Library身份/版本匹配成功回执。ZIP机械独审限定ACK：CRC、190项大小/SHA、13项product-source（7源码＋1测试＋3报告＋AGENTS/SPEC）与固定Git、完整204TRX、同完整测试源码红绿、911输入和17版本记录均核；包内无DLL本体，未冒称独立重hash二进制。本次证据提交仅三报告，产品源码与测试不变。

## 未验边界

G的native/runtime仍是明确结构性夹具；B五文件来自正常框架依赖构建，均不签Windows自包含可运行包。三native DLL、完整Windows runtime和真实Windows/medium/browser/Explorer/WPF/OLE/OCR效果/画质/250/600ms缺口保留。此片不提供canonical部署、前向发布、逆序补偿、真实注册安装/卸载或完整P7。

无新许可/依赖/凭据/安全权限/费用/真实模型调用，不操作用户电脑；不改main、不部署。Off、来源/密码/完整性、取消/清理/隔离/发送门、250/600预算及odd WebP拒绝不变。不能把计划封存或failed归档当安装成功。
