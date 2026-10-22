# 浏览器直启 NativeHost 的合作式生命周期门

从747d315e05c2c13e88d605efe133a6a5a2b942bc继续SPEC13.4/P7-04/05。Program保留原origin/安装信任校验顺序，进入BrowserSession前必须加入当前generation的正常受控App会话。未连接真实注册/安装入口，没有注册表、安装或系统设置动作。

## 实际接线

新空InstallationStore首次创建owner时才CreateNew空host-activity.lock。既有owned目录缺门、未知目录/字节都拒绝；不能自动补门然后假定旧版未参与协议的Host已退出。没有安全的自动旧目录迁移或未知进程接管。

角色admission继续持有原upgrade.lock；真实变更额外持有Host独占活动门。Apply、Recover、Upgrade/rollback/retry（包括原handoff重试）、Restore、FinishUpgrade及内部注册事务接入；传入role-only admission不能替代变更门。原setup installation.lock只在有界取得门与记录复核期间持有，不等待协议、子进程或Task。

正常控制器在原Start返回的App完成受控准入及READY后，在原完整admission/execution/proof仍有效时，CreateNew同目录prepared记录并完整写入、flush、关闭，再复核归属并以不覆盖目标的Move发布browser-host-session.json，含原canonical launch proof及exact generation/current/owner/launch。发布不是发送授权；仅原controller可精确退役自己的记录。正常原App实际退出后、原launch/owner完成前退役；取消/故障同样先退役，后结算原持有子进程。退役首先CreateNew原launch的closing记录，Host看到该文件或目录立即永久拒绝；随后只移动自己的精确session。原对象只有成功写/关闭closing后才记住归属，首次遇到已有closing即使bytes相同也不领养，显式重试只核原对象自己的exact记录。closing历史不清除，不影响不同launch的新正常会话。记录被替换则保留未知记录、原admission/owner和静态持有的controller，合并原错与退役错；显式Dispose只能重试精确原记录，不能擦foreign记录。未确认原App IO/退出时不完成owner。Publish/Retire已有原role upgrade lease排除全部合作式writer，不再争短setup锁；Move只发布已完整关闭的记录，Host入场前后与持续检查closing/session变化。退役专用RequireCleanupLease只证明原admission句柄仍持有，允许清理自己的exact session，不把foreign execution-owner记录当新权限；通用Require/RequireMutation/Publish均仍核完整owner。foreign owner及失败会话的持久fence保留。写/flush和关闭错误分别单错EDI、多错Aggregate，partial文件及不确认关闭的原stream保留，不假称已撤销或清理成功。

Host只打开既有setup和空活动门，在短setup段内取得共享门并绑定原session/generation/accepted proof和原admission/execution held lease；全包验证移出setup段，共享门继续阻止变更。成功entry静态保留到自身OS进程实际退出，**不在BrowserSession返回、断线、取消或托管Dispose时释放**。控制器不收养/终止浏览器创建的Host；有活共享门直接拒绝变更，让原生命周期自然退出。未参与门的旧目录/未知owner保留拒绝。

捕获的session检查是sticky：Offer/连接前后、Ready返回、Submit、每次Reply、每次实际Send/flush和分片前后、Commit均复核，退役/替换后永久失效，不能重绑新session。CancelImage仍可清理原grant。原Windows真实connected peer、origin、完整性、图片确认、SHA/画布/Worker结算和主Off/发送门都保留。普通未受控入口、旧StartActiveRole和升级handoff不自动获得正常browser会话。

这是产品合作式文件协议，不是同账户恶意进程防护；指纹、JSON、路径或持有锁不能替代真实peer身份或用户授权。共享/独占依据[FileShare](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0)及[Windows文件共享与句柄生命周期](https://learn.microsoft.com/en-us/windows/win32/fileio/creating-and-opening-files)。Windows实际共享/句柄行为本轮未运行。

## 原始验证与历史红门

原件artifacts/2026-10-22-browser-host-entry保留完整命令、日志、TRX/失败XML、执行前源码/输入/DLL摘要；轮次不相加。

- r01/r02是新增代码编译失误（误改ExecutionOwnership.Dispose；fixture接口漏成员），零执行，不作产品反例。
- r03初版新例8P2F，测试awaitusing Dispose的Busy遮住正文。r04增加正文/清理双轨保全后，原两例0P2F精确显示EOF返回3而非测试误期望0。保留产品原Disconnected语义，仅修测试期望。
- r05独审确定P2反例0P2F：只在Submit入口复核时，Begun等待或首块原write屏障后退役仍能完成Commit（原Submit无异常）。修复持续guard，不放宽协议/时间/断言。
- r06 15P1F，新增测试查询已正确finalize/Dispose的Process.HasExited，导致No process associated；改读原protocol在处置前记录的实际退出及IO结算证据。原清理安全门不改。
- r07/r08各17P0F0skip（中间候选）：覆盖原Host session返回但进程仍活、正常/非零实际退出、独占/共享顺序、READY前拒绝、缺/未知metadata、旧session永久失效及零Offer/Submit、取消保留owner、记录替换双错/精确重试、App原IO屏障、旧目录禁止自动迁移、Begun/首块屏障下零后续片/Commit及退役仍能Cancel。r08仅补新增fixture在失败路径释放原屏障、分别join原Submit/serve/原App run并保全多错。不是Windows或完整Core验收。

- r09冻结55959b3c01e7cee454f364d7cebf2cc771177201的相关回归514P31F/0skip，完整原件保留，不回填绿。27个注册夹具手造新store缺新活动门；2个ChangedExactRecords和1个InvalidBoundImage在健康receipt已到但session尚未发布时注入故障；1个正常capability例的rig.Fence与退役争setup。未盲重跑。
- r10独立确定P2反例0P1F：持setup并hold原App cleanup IO取消，会话退役Busy后新Host实际ENTERED，期望REFUSED。用closing先撤销、原role lease下原子发布/精确归档解决；不扩大时间预算。
- r11修复针对62P0F0skip（原31失败、相关新例及原回归的定向集合），880输入无漂移。注册夹具只显式构造新空活动门，不给旧目录自动迁移；3个原故障注入改等准确publication信号，全部正文断言保留。另补未知closing文件/目录及首次相同bytes也不得领养的检查，纳入最终固定轮。

定向生产/fixture差异与r08原件获独立只读限定ACK；r09失败后上述收敛设计也获限定ACK。仅固定修复树后的一次必要相关回归与App交叉构建可以作为最终签收，早轮不累计。

## 未验与未接入口

真实Windows句柄共享、浏览器直接发现和启动、同WPF/真实medium身份、注册表读写/关闭、Explorer安装升级卸载、断电耐久及对抗均NotRun。完整payload/路径派生metadata、稳定启动器、身份安装/撤销、自启动/安装器仍有缺项；本片不代签完整P7，也不声称旧版未知Host已退出。真实WPF像素/DPI/焦点/高对比、OCR/图像质量和250/600ms仍未验。本轮未重探403 runner、添加权限/凭据/依赖/许可/费用、调用模型或操作用户电脑。

## 修复固定树签收

产品/测试722270d46cd5ea6bcb7d0375ab652854b85506ae：r12一次同范围相关549P/0F/0skip，19个实际类，本片21新增+528既有；r09原545个测试名称全部保留通过。不是完整Core/Provider/Codec，也不把各轮相加。r13正常App交叉构建0W/0E、零运行测试。两轮880输入前后稳定并逐Git blob一致，Core/NativeHost/CoreTests/App四目标摘要与各轮产物库存匹配，AssemblyInfo含完整产品SHA。

348个原src中340个逐字节未改；8个既有src改动、2个新src。3个旧测试文件仅增加新生命周期fixture同步/原cleanup屏障或fresh-store活动门，原正文断言与用例保留；两新测试文件提供21例。工具只改既有BrowserHostFixture并加其本片合成模式。锁文件、依赖、工作流、packaging、extensions及scripts未改。原件/完整TRX、失败原文、输入/产物摘要与独立只读签收一并保全；最终只归档三报告再正常开发分支推送。

最终原件独立只读限定ACK：r12/r13、全部880输入、四目标摘要/完整版本及ZIP逐项核验一致，无新增确定P1/P2。原件Library `libfile_919bb999dad48191be09ed3585c278ef`，版本0；2,261,208B、308唯一成员/307项manifest，SHA256 `390f56f1e648ff5144eee6eb66dcdb3a91570e597938ae443e2412e293a6b3c2`，CRC/manifest通过，保存后本地身份与摘要确认。ZIP内报告是保存前固定快照；仅这三报告追加Library和独审回执，原ZIP及产品源码不改。
