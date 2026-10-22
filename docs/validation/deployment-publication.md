# 原 scope 前向发布与逆序补偿

基线Prepared产品9c2256774d70c970b4e1cd24aec7db0f2a2f12af/证据7072b06bfa80051a2db06d90c3126e32bcd9bee4，根侧限定ACK后实施。对应SPEC13.3/13.4、P7-04/05及统一事务设计。本片仅内部内核、隔离真实文件根与fake registry；不接真实注册或安装入口，不启动App或发布READY。

## 原 owner、任务与前缀

CommitPrepared仅接受同scope的原live Prepared及原snapshot/registry owner，一次保留publication即退休旧G/B/Prepared资格；不从JSON、路径、PID或摘要重造授权。原forward innerTask先登记再开放producer；其finally只通知同一个预发布finalizer，finalizer仅join原innerTask，不与observer互等。Abort同sync锁封闭producer，不等写锁或取消原IO。shared ledger新增最小publication channel：仅读安装根，写权限是本operation的三个发布文件、owner及固定temporary/retained文件；token为None保证已启动原IO结算，前向在各步骤由原scope取消门检查。

每步保留原admission/exclusive Host fence，先验exact Ticket/Before/Prepared，再沿原IO完整核新G/B和旧恢复目标。旧G由Before.Current的真实pin经PlanCurrent验完整v3；旧B由Before owner/active经RequireReceipt(io)验真实catalog/Core/revision且Generation==旧G，不借可为空的Host.Before。完整前缀包括current、B active、H receipt、owner、两个物理槽、已知temporary及候选映射；foreign、partial、unknown或原关闭不明锁存阻断，不以随后一次正常读取覆盖此前foreign结论。

B先在ticket下完成canonical归属准备，尚不激活：首次container须原先缺失且新目录为空，owner CreateNew/Flush/原Close/exact确认；inactive→canonical Move后完整验同原G/Core/revision。仅原bootstrapIO的metadata记录按原plan路径精确重定位，原候选凭据不改造成canonical授权。container/owner/Move任何失败均保留原件与pending，不猜成功、不删目录，也不继续发布current。新首次owner、已归属bundle在已知补偿后保留，并在terminal记明。

前向固定为G current→B active→Chrome→Edge→H receipt。每次写前登记attempt，真实write/move/replace抛错也只在原资源关闭确认后，以exact before/after观察识别可能已发生副作用。原registry adapter同一store单pending Task，读/写原Task先登记；关闭unknown时不新开key。普通已知write-after-effect错误仍可补偿；foreign验证失败则永久Unknown。H receipt写完仍为provisional，ticket持续阻止合作式准入。

## 补偿、Closing 与唯一成功点

finalizer先join全部原准备及forward，再运行BeforeClosing并核目标组合；最后同sync锁观察取消/Abort。Closing前失败/取消则在原admission仍持有时逆向H receipt→Edge→Chrome→B active→G current，ignore取消token但逐步验完整合法前缀。任一步foreign、关闭unknown、恢复目标损坏或补偿失败立即停止更早依赖写，保全原错和补偿/清理错。初装只恢复默认值而保留物理empty key；不删key，不称Windows原子CAS。

进入Closing后只结算原操作，不因新取消启动补偿。原IO、registry、admission分别close/保全错误；Closing新开的只读句柄仍由原finalizer任务持有并关闭。按该原Publication的已结算结果核目标或恢复组合，不复用只认Before的旧Verify。目标verified且全原资源结算后，≤16KiB CommitCandidate精确替换pending，最后Move到deployment-committed-operation.json才Committed；成功Move后无新的IO或可失败清理。已知回滚只归档AbortAfterPrepared，绝非安装成功。

Move报错且pending仍在为Unconfirmed；若它实际已Move，只能沿原finalizer只读识别exact archive并确认原新读也已关闭，报CommittedReceiptUncertain、保原错误，不重放、不回滚、不作为启动授权。历史读取关闭未知仍Unconfirmed，保留原owner；SettleOriginals只结算同一原对象，不重跑发布或归档。

actual终态对CommitCandidate以及所有允许的owner/canonical/empty-key组合在首次副作用前编码预检，16KiB不变。此项为防御性合同收紧；独审先前称合法包可达超限的P2已撤回，现无此真实越界反例，不造红或借计。

## 实际开发原件（轮次不相加）

artifacts/2026-10-22-publication记录每轮命令/日志/TRX/完整失败XML、当轮完整源码、输入及产物摘要。

- r01测试字段初始化的CS0649编译失败，零用例，已修明确初始化；r02七普通managed构建0W/E。
- r03新风险39例37P/2F：测试误将只读ReadActive当准入门，断言在stage callback失败触发合法补偿。改测真实EnterRoleAdmission精确Busy拒绝，未改生产门。
- r04真实foreign观测反例1F：原close后外部恢复旧值，旧Revert新读忘记foreign，实际误报AbortedAfterPrepared。r05 Launcher普通构建；r06因随后Core改变而Launcher副本未同步，失败Bootstrap/generation Core differ，非产品绿。原DLL摘要main dd30128180d1469259794f39333c74950840eca17c0c2291896f69088e01a360、Launcher5641d0da4b4dabc34dcb98b32644bcfe0e8604931faeaf887f544c94151f2f07不等，原件保留。r07正常Launcher同步后r08同完整testfile1P。
- r09七普通managed构建0W/E；r10新风险52P/0F/0skip，含七次真实合成child退出。该轮crash夹具虽实际结算成功，源码仍有另开exit wait/清理错可覆盖正文的问题；独审指出后已修保存同一originalExit/IO、逐一保全错误，未确认保留Process/tasks/root，不以绿例掩盖缺口。
- r11旧恢复目标反例2F：Seal后旧G或旧B损坏，旧实现仅恢复pointer字节而误报AbortedAfterPrepared。r12正常Launcher同步后r13同完整testfile2P；现每个prefix沿原IO复验旧G/B，失败Unknown并停更早恢复。

最终风险类54例包含初装/已归属升级、每个provisional阶段故障及取消、五反向失败/foreign依赖停止、三文件与两槽write-after-effect、B归属/Move失败、原registry write/read/close未知、metadata partial/close、两种Closing取消胜序、双端清理错误、归档前失败/收据丢失/历史新读关闭未知、sticky foreign、旧G/B损坏，以及七个真实child crash阶段。仅合成fixture/文件registry，非真实Windows。

内部只读设计及必要修复源码独审限定ACK。r10与r13不相加；签收以以下固定树单轮原件为准。

## 固定树相关签收

产品完整SHA `2ccc70074c1d586cd50acd853cc35f526918c711`。r14在该clean HEAD按原SDK/锁执行七普通managed构建，0警告/0错误、零测试。r15在同一clean HEAD一次相关Core测试258P/0F/0skip，258唯一显示名＝Publication54＋Prepared28＋B30＋G28＋Scope25＋Before23＋BootstrapDeployment35＋HostRegistrationTransaction35，即新54＋原204，不是完整Core，也不借记旧轮。

r15包含七次真实合成child自退出73，父侧等待同originalExit及原stdout/stderr全部结算；Crash前ticket持续阻断，Archive后只识别exact已提交历史，不收养或重放。生产仍只隔离测试根/fake registry，未调用真实Windows registry。

两轮915输入前后稳定且逐项匹配产品Git；17份AssemblyInfo包含产品完整SHA，14产品DLL正常构建与实际测试摘要一致，Launcher/Core.Tests/SettingsStoreFixture/BrowserHostFixture四实际Core副本相同。337旧测试文件逐字节保留；367旧src只改3增2，其余不变；Tools只改Program测试入口、增加明确crash fixture。锁/项目/工作流/打包/扩展/脚本未改。scope-audit.json保留类多重集、红绿同完整文件摘要、产物与Git核对；原件无DLL本体，不冒称包内独立二进制重hash。

固定源码与原件独立只读复审限定ACK，无确定新增P1/P2；审核未重跑产品。原件已保存Library `libfile_f64fba2fd0e881919089b06caae265af` v0，2,353,493B、287成员/286manifest，SHA256 `8177f07a91d61180f0ea6f93f3988d907431d989fa8fb8d79a08dbcc62164ffe`；本地Library身份/版本匹配成功回执。封包机械独审限定ACK：287唯一成员/CRC、286项大小与SHA、13项product-source（5源码＋1测试＋2工具＋3报告＋AGENTS/SPEC）与固定Git、raw258、七child记录、两同file红绿、915输入及17版本记录均核；r01/r03/r06旧失败保留。无DLL本体，不冒称包内独立重hash二进制。本次证据提交仅三报告，产品与测试不变。

## 边界

本片Committed仅指三方目标/原资源结算，非Windows可运行或健康证明。G native/runtime仍是明确结构夹具，B来自正常框架依赖构建；缺三native DLL及完整Windows自包含runtime。无真实注册/身份包/固定locator/安装卸载/自启动；真实Windows/medium/browser/Explorer/WPF/OLE/OCR效果/画质/250/600ms未验。crash child是受控Process实际退出，不是断电/NTFS持久性证明；registry read/check/write只合作式，无跨外部同用户原子CAS保证。

未改App/UI/Off/来源密码完整性/250600/取消清理隔离/发送及odd WebP/visibleCrop拒绝门；无真实模型、新许可依赖/凭据/安全权限/费用或用户电脑操作，不改main、不发布部署。
