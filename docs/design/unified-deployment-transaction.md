# Generation / bootstrap / Host 统一事务内核设计

状态：DesignReview，未实施。基线产品aab208ab716f5c1b36c0392d464ff35d8fc1dd05，当前证据6f6fa4cc3ef121831d53235cb3563a062056ef1e。对应SPEC13.3/13.4、P7-04/05；本片只设计内部“部署并注册”内核，不接CLI、安装器、固定locator、身份注册或自启动。

## 一份原 owner，三个参与者

协调器拟放`KuaiYi.NativeHost/InstallationDeploymentTransaction.cs`：现NativeHost已经能访问Core内部类型和自己的`IHostRegistrationStore`，不反向引用NativeHost、不新增项目/包依赖。Core新增仅供内部使用的`InstallationDeploymentScope`，工厂取得一次原mutation admission（exclusive Host activity + upgrade lease），发布原Run Task后才开始工作。scope持有原admission、原任务、journal字节与每次写入尝试；参与者任务producer先封闭再取结算快照，不把WaitAsync外壳、首个WhenAll异常或“当前列表已完”当原工作已完；不能从磁盘JSON、PID或摘要重新构造scope。参与者只借用scope，不关它、不自取admission、不自行宣布commit。

前置为已静止的合作式store：现存execution owner、browser-host session、未知pending或未结算Host均拒绝；已有控制器必须先由其原owner按原退出流程结算，内核不扫描/收养/杀进程，也不接受布尔值代替退出证据。初装只允许既有Store规则认可的空专属根；创建owner/零字节锁是store初始化，不是部署成功。配置与加密数据目录保持分离且不写入。

| 现有基元 | 可复用 | 必须拆出的共享scope路径 |
| --- | --- | --- |
| `InstallationStore` / `InstallationPackage` / `InstallationPayload` | 源SHA、v3物化、角色与派生完整性、随机generation、exact current发布/恢复 | 准备v3但不发布current；之后单独Publish/Restore。现Apply在setup锁内跑health，不能在其callback调用另两入口 |
| `InstallationBootstrapDeployment` / Package / Entry / Selection | 不可变五文件+catalog、Core/revision闭包、4K/8K/16K容量、exact active、迟到控制器门 | 对“已准备的新generation”验证Core；借用scope准备/发布/补偿，不要求它已成为current，不建立第二份私有pending |
| `HostRegistrationTransaction` / `WindowsHostRegistrationStore` | 两个物理HKCU默认值、原raw key关闭、foreign拒绝、default补偿及empty-key保留 | 快照旧receipt/旧generation，生成指向新generation的plan；允许原旧归属→新归属的受控替换，不能继续用“receipt必须等于当前新generation”的单步注册校验 |

原三入口保留独立行为，共享路径抽取同一校验/写入帮助函数；不通过`skipChecks`、可伪造的“已取锁”布尔或外部回调授权绕门。共享scope方法每步Require原lease、exact ticket/plan与该阶段允许的前缀状态。PreparedGeneration只能由当前scope的完整v3校验工厂产生，不把源目录或手造InstalledVersion当已安装候选。

## 一个持久阻断与有界计划

统一阻断直接使用既有`upgrade-pending.json`，新schema/kind使旧协调器拒绝解释，所有既有合作式入口仍可凭“存在”拒绝。不用仅新版本认识的新marker留下准入空窗。普通App、bootstrap、Host与原三个独立变更入口始终拒绝此ticket；只有内存中的原scope可操作自己的exact字节。

1. **Ticket**：取得scope后，任何参与者写入或registry读取前，以CreateNew/Flush持久化；含operation、root/data、两源catalog SHA和预留generation/bundle ID。仍≤16KiB；部分写入不删除，未知字节继续阻断。不能直接用失败时删除partial的`SettingsStore.WriteFlushed`写这些归属元数据；需scope持有原FileStream的CreateNew/Write/Flush/Close帮助函数，逐错保全、关闭未知保留同一owner。共享路径的ticket/plan/owner/receipt均用此函数，既有独立入口不借机改语义。
2. **Before plan**：只读快照及原读句柄结算后CreateNew，含原current、bootstrap owner/active、Host ownership receipt和Chrome/Edge的KeyExists/default/foreign状态；≤128KiB，绑定ticket SHA。
3. **Prepared plan**：完整准备并验证后CreateNew，含新generation源/派生catalog区别、新current、bootstrap owner/active、两默认值与新Host receipt的精确预期；≤128KiB，绑定ticket与Before SHA。完整payload不塞入journal。
4. **Terminal candidate**：≤16KiB，按下表绑定存在的计划摘要及Commit/Abort候选结论；不保存原文或异常Message。terminal写入/替换/归档属于预先发布的原finalization任务，返回前join，不向已封闭参与者集合迟到追加背景Task。所有原参与者清理确认后以同owner完成其CreateNew/Flush/实际关闭，再以exact File.Replace把它换入upgrade-pending（原ticket保留在固定operation侧文件），最后才Move到committed/failed archive。替换本身仍保持阻断，不是成功点；归档内容因此绑定完整计划，不能只留下缺少Prepared摘要的早期ticket。

Terminal必须显式携带variant，不能用缺省摘要假装准备成功：

| variant | Before / Prepared摘要 | 允许条件 |
| --- | --- | --- |
| CommitCandidate | 必须 / 必须 | 完整目标组合verified，原任务/句柄结算完成 |
| AbortBeforeSnapshot | 必须null / 必须null | 只完成ticket；预检拒绝、无可选记录/registry发布，原读取与关闭均结算 |
| AbortBeforePrepared | 必须 / 必须null | 准备失败；current/B active/registry/Host receipt均未发布。允许保留intent已归属但未激活的候选目录及完整首次owner，终态记其预留ID/owner摘要，不冒称候选完整 |
| AbortAfterPrepared | 必须 / 必须 | 前向未开始或已按前缀反向恢复逻辑before；保留empty key/候选归属差异 |

任何ticket/plan/owner/receipt的partial元数据写入或原关闭不明仍Unconfirmed，不走上述已结算Abort路径；候选payload的partial复制只有在原IO确已结算且路径属于本intent预留范围时，才可保留归属并作AbortBeforePrepared。Before尚未完成但发生了本设计不允许的参与者写入，同样不能降级为AbortBeforeSnapshot。

各字段维持原4K/8K等消费上限；actual序列化与组合START容量全部在对外发布前核对，不扩大IPC/250/600ms预算。

读取任何未知、超限、不canonical、被替换的ticket/plan直接Unconfirmed。若预检发现foreign且尚无发布，只能在所有原读取/关闭确认后归档failed；关闭未知则保留ticket与同一owner。ticket或plan写入失败时也不把partial metadata清除为“没发生”。

## 准备、前向发布、唯一成功点

顺序固定，发布期间ticket始终存在：

`Acquire → Ticket → Before → Prepare(G,B,H) → Prepared → G current → B active → Chrome → Edge → H receipt → Verify → Closing → Commit archive`

- G在预留随机路径物化完整v3，保留源SHA与派生SHA，verify后发布不可变目录；current尚不改。
- B针对该候选G准备不可变目录；源Core必须与候选G的实际Core相同。首次bootstrap owner创建也在ticket下，before/after均明确；新marker和候选目录失败时保留归属，不能收养未知现有目录。
- H只准备旧/新默认值与receipt；旧注册必须由旧receipt及旧generation确实拥有，不把同路径、摘要或空默认值当授权。两浏览器是物理槽，不将WOW64 views计为额外槽。
- 全部Prepared持久化且原准备任务已结算，才按固定次序改可见记录。每个写尝试先登记，再执行同步原IO/registry调用；即使调用抛错，也以exact before/after观察处理可能已发生的副作用。registry close未知时禁止继续读/写新key。
- 新H receipt此时也只是**provisional**，不能沿用现单步Host事务“receipt已写就不再补偿”的局部commit语义。ticket仍阻断App/UI/Host；内核不在此阶段启动UI、发布READY或发送图片。
- Verify检查完整目标组合和所有原IO/registry资源。进入Closing前最后一次观察取消；进入后不再因新取消启动补偿，必须结算同一原操作。Closing不是成功点。
- 原registry key owners、原参与者任务和原admission全部实际关闭确认后，ticket仍阻断新准入；只核exact计划状态、把已结算的Terminal candidate exact替换入pending，再将pending原子Move为`deployment-committed-<operation>.json`。**只有这次Move成功才返回Committed**，无后续变更或可失败的清理工作。

Terminal替换或最后Move失败/结果未知：不返回成功，也不在已关闭admission后假装仍持锁补偿；保留ticket/原scope，返回Unconfirmed。仅同一owner可在原ticket/terminal两种精确已知字节状态中重试最后的只读校验、terminal替换/归档，不重放三方写入。若Move抛错而pending已消失、exact committed archive存在，标为CommittedReceiptUncertain并保全原错误，只识别该operation的历史结果；不重建pending、不回滚、不再跑参与者，也不把历史记录当当前启动授权。pending/归档都不符合已知字节时，只报存储结果未知并保留可确认原件，不猜测成功。进入Closing后的取消不能把已完成commit谎报为回滚；receipt丢失不自动重试或cold fallback。

## 失败、反向补偿与崩溃

Closing前的失败/被观察的取消：先join已启动原任务，再在同一原scope内反向恢复`H receipt → Edge → Chrome → B active → G current`。仅撤销已尝试且实际处于本plan before/after状态的资源；每步重核原ticket/plan、owner及合法前缀。首次注册可能留下空key：只恢复默认值，保留空物理key，不称物理树逐字节回滚。新G/B目录、staging与已精确归属的首次owner marker保留；不删除未知锁、目录或用户数据。

任一反向步骤遇foreign/关闭未知/补偿失败，停止更早的依赖写入，收集原操作错、补偿错及全部已启动原清理错，保留ticket、两计划、所有目录和原资源owner。单错EDI、多错Aggregate；不让finally或WhenAll抛出的单个错误覆盖另一端。不以新开的handle或lease证明原资源已经关闭。

按对应variant验证无发布或全部反向逻辑状态，且原资源结算后，才以Abort terminal替换pending并归档为`deployment-failed-<operation>.json`，返回RolledBack/Refused并保留原错误；这两者不是安装成功。若最终failed归档失败同样Unconfirmed，不解阻断。

进程崩溃：commit archive前残留pending（包括Terminal已换入但未归档）一律拒新准入；新实例只读报告恢复状态，不按PID/相同SHA收养、自动清journal或重放。commit archive后崩溃只可能是成功收据未交付；operation的历史已提交记录不是当前启动授权。重复operation在任何写入前拒绝；读取历史状态不触发再次部署。显式跨进程修复/全安装器恢复入口另设计。此次合成crash只证明受控进程退出后的fail-closed，不代签断电/文件系统持久性。

## 最小验证矩阵（待实施，均非已通过）

| 场景 | 必须观察的结果 |
| --- | --- |
| 初装、旧三方已归属升级 | 同一scope贯穿；新G/B/H一致；旧目录字节保留；仅一个Committed点 |
| G/B准备copy、物化、校验失败 | 无current/registry提前写；失败候选留精确归属；不触用户数据 |
| current、B active、Chrome、Edge、H receipt各发布后失败/取消 | 精确逆序补偿；原旧Host receipt可恢复，不能因局部receipt已写跳过 |
| 每个反向步骤失败或被foreign替换 | 原错+补偿错完整；不越过依赖继续写；ticket持续阻断 |
| registry写/flush/close原任务被屏障hold | 取消不等writer锁；Run不丢原任务；不以新Read/新handle声明清理完成 |
| 最后Close前/后两种取消胜序 | 前者补偿；后者原结算继续，成功才Committed，不扩大超时 |
| 原admission、registry cleanup抛错，双方同时错 | 同一owner保留、无commit archive、错误对象保全 |
| Terminal替换、commit/failed archive失败或收据丢失 | Unconfirmed/历史Committed准确区分；无自动重放、重选或fallback |
| 每个持久阶段受控子进程自崩溃 | 原parent观察实际退出；ticket前/后分类正确；重启不收养未知owner |
| 入场与部署/最终Closing空窗竞争 | 旧/新Launcher及Host均被已知upgrade ticket阻断；无混代UI Start |
| 错源SHA、源/派生混用、Core混代、metadata/wire超限 | 发布前拒绝；不靠新摘要放宽闭包、4K/8K/16K限制 |
| registry不存在→创建→失败 | 默认值恢复，empty key保留；不是整key删除，也不冒称Windows CAS |

测试用真实文件Store/不可变bootstrap和现有两跳/Host合成进程；registry仅中性合成槽及原句柄屏障；进程崩溃验证需要能跨测试child退出保留的文件合成槽，明确不是Windows注册表。先新风险定向红绿与必要旧独立入口回归；不重跑旧310/586凑数。按实际参与者变更做正常managed交叉构建。产品SHA冻结后一次相同范围签收并保留原件，最后正常开发分支推送。

## 明确不签的边界

本内核Committed只指三方文件/注册目标与原资源结算，不是新版本真实可启动/READY健康证明。初版仍限框架依赖开发组合；原三native DLL及完整自包含runtime缺件、真实Windows/medium/Explorer/WPF/OLE/OCR效果与250/600ms未验。无固定locator、身份注册、Inno Setup运行、自启动或真实安装卸载。Windows registry不是跨外部进程原子CAS，不保证非合作的同用户写入不会在最后观察后变化。后续真实安装器还需可运行payload、原会话健康/回滚集成与各自平台证据，不借本设计代签P7。
