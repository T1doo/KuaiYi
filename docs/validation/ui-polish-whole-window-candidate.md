# 下一次完整WPF窗口候选（未触发）

当前目标是结束真实界面/动效/自有进程链的验收缺口，不再增加启动前微诊断。基线4c0a1a64a6c70415fbeb97ae2b2ec021777f9e05；固定before e4d57e77426ab235c7219b375debd76bc072e00b / after e12c1ed8ff351f8300dbdaca18585d7994493317，before本就已有暖橙UI。产品、C#探针及原Full/Reduce几何＋fade条件未改。当前准备分支不匹配CI执行条件，不push执行分支，不新跑Windows。

## 下载即用的真正门槛

当前packaging/installer-source-pins.json明确Unavailable，generationSha256/bootstrapSha256均null；实际八角色托管publish只是未封存输入。Launcher.Program走InstallationBootstrapEntry.Open，要求原bootstrap-bundles/GUID及已选代/owner/active绑定；平铺publish目录不是可直接冒充该布局的安装成品。原G/B/origin及可信安装输入仍要闭合，不能仅将目录/receipt/hash当来源。历史四native曾构建并回收，但本环境的测试临时目录DLL不能作为受审生产封存输入；本候选不跑或重开旧4DLL窗口。

普通启动的StartupSources/登录启动登记来源另受原Installer及原作用域生命周期约束；本轮不另造issuer、不抬pins、不生成trusted=true，不宣称这一权限链已交付。最短交付路径仍是受审输入封存G/B→原安装/正常启动链验收→真实可交付包；本次完整视觉窗口只解决其中UI和资源退出证据，不能替代包或来源资格。

## 一次完整接线

原已注册windows-ui-polish.yml恢复为完整窗口，仍固定原仓库、r03执行分支、自身path、公开push/attempt1和contents:read；无dispatch/matrix/retry。checkout原固定SHA、不持久凭据。Python parent门更新为精确基线和本片完整十文件差异，clean/head/原Git blob/大小/路径/逐hash断言不放宽。保留既有官方SDK10.0.402/runtime10.0.13、验签/locked restore/audit、两份真实原源码树及真实Windows构建；不是借旧产物或诊断成功代编译。

生产harness仍加载实际App模块版本及hash，来源提示走原快照。先before+after设置，再before+after浮窗，四原进程各持有原wait/双流/关闭状态；每次launch前原证据条件及总预算不足即停。设置每侧16张：浅深、宽窄、真实程序focus/disabled、原Combo独立Popup HWND/DPI、F4/Escape/方向键/Enter程序路由及恢复原草稿。浮窗每侧18张：Full/Reduce、生成/完成/停止状态，Full仍必需同请求epoch的真实中间几何＋fade，缺证仍Fail。计划共68图，不是已取得；非物理键盘/真实桌面抓取/DWM或系统DPI验收。

原Job对象覆盖自有supervisor及其prepare/run、原harness和继承后代，默认无breakaway；初始化失败、发布失败或UNKNOWN都不丢原owner、不换observer、不按PID收拾。Job成员查询/原句柄关闭、四窗口/tray/Flow与IO各自结算；checkout/upload/runner不属于该Job，父前台返回另记，不能用runner orphan日志称自主清理。保持已披露HKLM EnableLUA只读桌面预检；无注册表写入、安装/卸载、系统DPI/theme/motion/安全变更，非用户电脑、模型或实际捕获。

整个job≤15分钟。为失败冻结/校验/上传留余量，supervisor原等待总额≤12分钟，prepare≤6、视觉总计≤6；原Native步骤外层≤13分钟，纯FS校验≤1分钟。外层到期若原wait/关闭仍不确认，保持UNKNOWN，不自动延长或重试。该降低准备预算不将失败变绿；官方网络/构建若超时则记录实际失败。

## 本轮owned证据与失败保全

父步骤先确认native、publication及父收据命名空间均不存在，再exclusive取得当前run/attempt/fullSHA的父根，关闭并写本轮namespace资格后才启动原supervisor。原前台返回和退出码另记，写证/关闭独立结算。原work_acquired之后才可冻结；Job构造/assign失败的当前新根只给UNKNOWN，不能假称句柄已闭。

新的visual_publication只做当前自有文件IO，不是另一套来源/授权系统。原最后report保存后，固定原root JSON/log与四个合成view目录完整保留文件，排除SDK/包/源树/二进制和冗余旧phase副本。数量≤512、单文件≤32MiB、PNG≤2MiB、总额≤192MiB，边复制边限额。拒链接/重解析/硬链接、路径逃逸、未知文件类型；仅另放行原SettingsStore/SettingsCoordinator在固定合成state路径创建的两个已知空锁文件，非空或其他锁不发布、不删原件；文本签名/credential查询（含标准AWS/Google签名键）/私钥/PAT扫描跨块，命中即整包不发布，不改写或删除原件。副本逐hash复核、原文件前后长度/identity/hash和最后集合/hash复查后，最后才生成manifest/READY。

READY仅表示这份冻结快照完整可核，绝不是测试通过。UNKNOWN只能保留UNKNOWN失败快照，文件稳定不证明原任务已结算或全局原生状态原子一致。发布/收据失败不能绕过原owner hold；部分publication根保留但无READY不上传。原任务仍可能改写而任一复核不符时拒绝发布，不用复制反推清理。

always校验步骤只验证已冻结副本，不再追读或重收原Native输出；本轮namespace资格、完整identity/manifest/文件集合/大小/regular约束均通过后才给artifact输出。任何旧根或资格缺失都不验证/上传；安全父投影严格schema且≤4KiB，不收额外字段。原前台未返回时只表NotObserved，不以缺parent-exit证明清理。冻结失败至多保全本轮安全父回执；原始输出不删除、留原runner，但不能冒称已永久下载回收。

未来artifact有效期1天；原始新JSON/log/PNG及各自manifest均是新运行数据，不是改路读取旧403日志或新404收据。已知对象存储读取曾Forbidden，若新图包正常下载也受拒必须停止，不换工具/账号/端点或把无图当验收；须有权提供本次图像原件才能做人眼像素审阅。不能用另一次窗口追同份受拒内容。

## 当前离线证据与批准边界

发布器15项、原owner相关21项、原窗口合同28项、UTF8入口5项、保留诊断源码hash正例1项均实际通过；它们仅离线。发布失败仍hold、签名查询跨块、源变更、旧根、links/类型/数量/大小、图包变更/额外文件、identity/父字段注入均有区分反例。C#与产品未改、不重新编译或重复旧15项数字。原生PowerShell模板、Job API/WPF与68图仍NotRun，旧403首因Unknown、404成功仅启动前范围不扩大。

独立只读审查后可正常推准备分支（不触CI），执行分支仍停实际49诊断。要执行本候选，需另行具体批准一次上述完整15分钟/6分钟窗口，并先核无延迟run/在途、精确候选HEAD/parent/十文件及来源。不会借先前只启动前批准触发。执行后根据实际结果和本次owned原件再独审；没有原件或未通过不能发布下载即用成品。

整包独审发现签名查询键漏扫后，新增跨块标准signature/X-Amz/X-Goog反例；原13方法中该新增方法的5个合成子例均红，补明确键后13项全绿。未改写原件，匹配后整包无READY；旧12P原件保留、不累加，也未为此重跑其余已通过用例。

实际设置源码的LoadAsync/TransactionLease会保留settings.lock和secret-transaction.lock空文件。对照原Coordinator命名后，补固定合成路径与零长度类型合同，原新增空锁正例报错→修后发布器15项全绿，非空/其他锁仍拒绝。不是放宽未知二进制，也不是删除锁来造绿；前述13项阶段原件分列保留。
