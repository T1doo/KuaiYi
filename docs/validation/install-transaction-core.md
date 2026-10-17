# P7 安装升级文件事务核心与剩余缺口

从父独审已准入的1341b12248d972f197ed4c57032a9137fb890d57正常建立dev/install-transaction-core，核实际HEAD/默认远端/祖先/clean/单worktree后写入。先只读核对原SPEC与已有模块，随后按父补充授权实施P7-04/05的独立文件事务子项；不是Windows安装器交付，未执行注册、安装、签名或部署。

## 原范围核对

| SPEC要求 | 原有源码及具体缺口 | 本片范围 |
| --- | --- | --- |
| 13.3 每用户完整自包含x64包 | text-alpha只含部分托管角色，installer=false、原生产品DLL未含；无Inno安装器工程/统一发行清单 | 验证调用方选定的包目录和固定catalog摘要；不生成发行安装器 |
| 身份包与实际安装位置、签名分离 | development manifest声明App/ImageWorker；package-identity的Release明确阻断；register-dev-identity遇既有包拒绝替换 | 不改身份/签名/证书/注册；不生成信任回执 |
| Chrome/Edge宿主与实际扩展ID | InstalledRoleFiles/IntegrationInstalledFiles及InstalledHostPolicy已读取/校验，未有统一安装写入链 | 保留现有角色/完整性合同，不把本片文件记录当NativeHost信任授权 |
| 可选登录启动 | 未见产品启动注册/统一撤销实现；集成页只有手动只读状态 | 不增自启动或权限 |
| 13.4 退出、统一升级、失败回可启动版 | Shell有本地Closing清理；Program没有安装器退出命令，现有单实例消息只Show。没有统一升级事务 | 实现完整文件集版本发布/原子当前记录/失败回退；真实停止与Windows启动健康检查未实现 |
| 卸载、修复、数据保留 | 无统一卸载/修复链；设置/加密秘密已在LocalApplicationData/KuaiYi独立目录 | 不删除卸载或旧版本；只实现文件损坏恢复及数据隔离保留 |

完整P7仍须补：真正安装器调用与完整锁定payload、实际退出/禁止角色在升级间重入、同身份与宿主注册回滚、实际扩展ID与安装来源、可选登录启动、仅本产品卸载/数据选择、干净WindowsVM的K01—K08。现环境无获准可用Windows，正式签名材料/开发信任授权未具备，原生完整payload及图像解码许可也未齐；临时目录结果不能抬这些状态。现traceability整任务状态不改，本片文件子项由本报告及progress独立说明。

## 实现及实际文件语义

新增Core/Installation两文件，复用SettingsStore.WriteFlushed的刷盘创建及RejectLink；未重复实现现有NativeHost校验或颁发安装权限。API要求调用方给定catalog SHA256；严格检查KuaiYi/schema/version、清单字段与重复项、跨平台相对路径/保留名、4096文件/单文件256MiB/合计1GiB上限、精确目录文件树及每项字节数/摘要。当前读取的是本地选定目录，不下载、不解压、不验证签名。摘要只证明与调用方固定内容一致，不证明发布者可信。

先流式复制并核验新完整版本目录，再以同一根目录内File.Replace/首次File.Move原子发布current.json。该记录含当前/前一版本引用，ReadActive真正读取并核验被选完整文件集；旧版本不原位覆盖，避免此核心读到混合版本。现有App启动和Windows注册尚不消费该记录，未来安装器需明确调用/集成，不存在“正常产品已原子升级”的声明。

发布后失败会重新核验旧版本再恢复旧记录；回退本身未确认则抛InstallationRollbackException并保留状态/两版本，调用方需读取实际结果再决策。初装失败将自己刚建的current记录移到failed-current旁证，不删除未知内容。Recover只在current可解析且活动文件损坏、前一版完整可核验时回退；未知owner/非空锁/未知当前记录或两版都坏均保留、不伪成功。已确认提交后单纯取消不冒称未提交。

安装根/用户数据根/源包根须隔离；事务不读取、迁移、解密或重写设置与秘密。required rolesStopped回调是调用方必须持续维持的前置合同，当前没有真实Windows停止/租约适配器。临时fixture的payload是公开文本字节，未有产品EXE运行，故其停止条件真实为满足；不把该回调称为真实进程结算证明。

## 红绿、回归和原件

独审指出新候选活动清单标量类型错误绕过回退：真实A→B目录中改B清单为schema字符串/溢出、version数字、path数字、bytes字符串/溢出、hash数字，r02实际7F；另Linux临时目录额外ASSETS与assets是两个目录，旧校验错误接受，r03实际1F。仅把纯JSON转换异常归为InvalidData，并用seenDirectories拒绝别名，r04相同八名字8P；两测试方法字节一直保持到最终r05。它们是本片候选缺陷红绿，不是历史Windows/图片/idle根因。若文件系统把大小写别名解析为同一目录，该用例验证正常包；本轮Linux实测为两个目录的拒绝分支。

r01为新测试27P1F，误期待IOException而实际InvalidDataException，仅把新预期改为精确类型，原件保留，不算产品红绿。最终r05专项40P；r06无filter完整Core1743P=父1703全名多重集+新40；r07无filter完整Provider2325P与父全名多重集相同；r08仅3个有界驱动绑定补执行；r09/r10实际App/Worker各0W/E，均0失败/跳过。包括实际并发读者跨发布只观察完整旧/新版、真实排他文件租约、停止条件撤销、提交前后取消、真实文件故障、未知内容与不确定回退保留。

三个checkpoint使用原SettingsStoreFixture新增的测试专用模式，在自有子进程VersionPublished/BeforePointerPublish/PointerPublished处Environment.Exit(73)，真实退出且绕过finally；新实例读/恢复的版本精确为提交前旧版或提交后新版。TRX保留exit73、公开marker、stderr0及实际driver DLL SHA。测试正常绿色路径均等原stdout/stderr与退出；任意早期失败的finally只kill已知自己创建的child并等退出，IO任务结算未全面证明，不扩大声明。

加密数据用真实SettingsStore/SecretFileStore及明确合成AES-GCM保护器建立；所有原配置/密文摘要/引用在成功和失败后保持，仍可通过原store解密。不是WindowsDPAPI、ACL或真实用户秘密验收。

封存自检发现r01—r04各35、r05—r07各37份源码采集遗漏修改的tools驱动Program.cs。原metadata不改、不倒填；r08起明确加入Program及fixture/Core/CoreTests项目，41份预执行源码快照，三例补执行的实际driver DLL与先前完全相同，child Core DLL也与测试Core匹配。最终六执行源码绑定合计37×3+41×3=234，完整联合源码41；不能称每轮都事前捕获了41份。其他预存DLL只是观察，不代表执行。封存索引另记精确作用域及勘误。

独立只读源码复审已无P1/P2，最终封存另审；[全部命令、TRX、失败源码、绑定勘误与回执](../test-evidence/2026-10-17-install-transaction-core.json)。父全部旧证据、既有产品源/测试、SPEC/原书/trace/锁/workflow保全，唯一既有源码改动为明确测试驱动分支；新安装核心和测试独立新增。

## 未验边界与截图主链

File.Flush(true)及进程中断证据不等于断电/目录fsync耐久性，也不等于Windows安装事务。文件集摘要完整不等于实际可启动。逐层链接检查及锁没有证明抗同账户并发换名；单包上限不等于累计磁盘上限，旧版/失败暂存不自动GC。本库不卸载、不删除未知文件/版本目录；复用的WriteFlushed仅在写入失败时删除其刚CreateNew的部分文件。测试teardown仅删除自有合成临时根。

截图当前已有显式最终裁剪→本地预览/本次风格→确认→双协议分类/生成及失败重新截图的托管链；仍无真实Windows可见链/焦点/锁屏/物理250600/模型质量证据，历史间歇原因各Unknown。原SPEC实质余项还有：Auto尚未联动本地OCR（现为单独显式OCR-only入口）；非法分类JSON当前按已验安全合同停止，而SPEC7.4要求降级解释，改变第二次发送条件须另定语义，未悄改；图片打开/投放与多格式管线受Skia四包许可/403限制，同图重生成仍依复用Off。未发现新的实证表明这些就是历史图片间歇原因，本片不再叠截图测试。更早P1—P5的真实WPF/UIA/OLE/DPI门槛及整体P8仍按原独立证据未验，不能因托管回归替代。

仅已有SDK10.0.401 --no-restore，激活前后禁生成证书；无新包/许可/Skia安装、403或Actions先前拒绝查询绕过、main/强推/部署/费用/真实模型凭据/签名证书/安全权限环境改变。未停止未知进程，未知锁文件数据日志保全。
