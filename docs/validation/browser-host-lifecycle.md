# 浏览器直启 NativeHost 的合作式生命周期门

从747d315e05c2c13e88d605efe133a6a5a2b942bc继续SPEC13.4/P7-04/05。Program保留原origin/安装信任校验顺序，进入BrowserSession前必须加入当前generation的正常受控App会话。未连接真实注册/安装入口，没有注册表、安装或系统设置动作。

## 实际接线

新空InstallationStore首次创建owner时才CreateNew空host-activity.lock。既有owned目录缺门、未知目录/字节都拒绝；不能自动补门然后假定旧版未参与协议的Host已退出。没有安全的自动旧目录迁移或未知进程接管。

角色admission继续持有原upgrade.lock；真实变更额外持有Host独占活动门。Apply、Recover、Upgrade/rollback/retry（包括原handoff重试）、Restore、FinishUpgrade及内部注册事务接入；传入role-only admission不能替代变更门。原setup installation.lock只在有界取得门与记录复核期间持有，不等待协议、子进程或Task。

正常控制器在原Start返回的App完成受控准入及READY后，以CreateNew发布固定browser-host-session.json，记录原canonical launch proof，含exact generation/current/owner与原launch。发布不是发送授权；仅原controller可精确退役自己的记录。正常原App实际退出后、原launch/owner完成前退役；取消/故障同样先退役，后结算原持有子进程。记录被替换则保留未知记录、原admission/owner和静态持有的controller，合并原错与退役错；显式Dispose只能重试精确原记录，不能擦foreign记录。未确认原App IO/退出时不完成owner。

Host只打开既有setup和空活动门，在短setup段内取得共享门并绑定原session/generation/accepted proof和原admission/execution held lease；全包验证移出setup段，共享门继续阻止变更。成功entry静态保留到自身OS进程实际退出，**不在BrowserSession返回、断线、取消或托管Dispose时释放**。控制器不收养/终止浏览器创建的Host；有活共享门直接拒绝变更，让原生命周期自然退出。未参与门的旧目录/未知owner保留拒绝。

捕获的session检查是sticky：Offer/连接前后、Ready返回、Submit、每次Reply、每次实际Send/flush和分片前后、Commit均复核，退役/替换后永久失效，不能重绑新session。CancelImage仍可清理原grant。原Windows真实connected peer、origin、完整性、图片确认、SHA/画布/Worker结算和主Off/发送门都保留。普通未受控入口、旧StartActiveRole和升级handoff不自动获得正常browser会话。

这是产品合作式文件协议，不是同账户恶意进程防护；指纹、JSON、路径或持有锁不能替代真实peer身份或用户授权。共享/独占依据[FileShare](https://learn.microsoft.com/en-us/dotnet/api/system.io.fileshare?view=net-10.0)及[Windows文件共享与句柄生命周期](https://learn.microsoft.com/en-us/windows/win32/fileio/creating-and-opening-files)。Windows实际共享/句柄行为本轮未运行。

## 原始验证与历史红门

原件artifacts/2026-10-22-browser-host-entry保留完整命令、日志、TRX/失败XML、执行前源码/输入/DLL摘要；轮次不相加。

- r01/r02是新增代码编译失误（误改ExecutionOwnership.Dispose；fixture接口漏成员），零执行，不作产品反例。
- r03初版新例8P2F，测试awaitusing Dispose的Busy遮住正文。r04增加正文/清理双轨保全后，原两例0P2F精确显示EOF返回3而非测试误期望0。保留产品原Disconnected语义，仅修测试期望。
- r05独审确定P2反例0P2F：只在Submit入口复核时，Begun等待或首块原write屏障后退役仍能完成Commit（原Submit无异常）。修复持续guard，不放宽协议/时间/断言。
- r06 15P1F，新增测试查询已正确finalize/Dispose的Process.HasExited，导致No process associated；改读原protocol在处置前记录的实际退出及IO结算证据。原清理安全门不改。
- r07/r08各17P0F0skip：覆盖原Host session返回但进程仍活、正常/非零实际退出、独占/共享顺序、READY前拒绝、缺/未知metadata、旧session永久失效及零Offer/Submit、取消保留owner、记录替换双错/精确重试、App原IO屏障、旧目录禁止自动迁移、Begun/首块屏障下零后续片/Commit及退役仍能Cancel。r08仅补新增fixture在失败路径释放原屏障、分别join原Submit/serve/原App run并保全多错。不是Windows或完整Core验收。

定向生产/fixture差异与r08原件获独立只读限定ACK。r08后仅Program保留原Policy拒绝顺序和测试writer-held精确Busy断言有差异，须由最终固定轮覆盖；固定树后只做安装/浏览器IPC相关必要回归与App交叉构建。

## 未验与未接入口

真实Windows句柄共享、浏览器直接发现和启动、同WPF/真实medium身份、注册表读写/关闭、Explorer安装升级卸载、断电耐久及对抗均NotRun。完整payload/路径派生metadata、稳定启动器、身份安装/撤销、自启动/安装器仍有缺项；本片不代签完整P7，也不声称旧版未知Host已退出。真实WPF像素/DPI/焦点/高对比、OCR/图像质量和250/600ms仍未验。本轮未重探403 runner、添加权限/凭据/依赖/许可/费用、调用模型或操作用户电脑。
