# 原失败会话到新三方补偿 Ticket（实现合同）

依据 SPEC13.4，接续产品b036/证据584c。只隔离文件根/fake registry/原合成受控会话；无新安装入口或真实用户注册。前片191＝新22＋旧169，不是旧262全回归；旧Prepared等204范围本片未借记通过。

1. **私有一次授权**：仅原Scope绑定的原Session，Completion=RollbackRequired、从未接受READY、原角色/Start/协议/Host退役与原读取全部结算，并且原controller仍持有同一execution对象/记录/lease，才能创建一个新补偿owner及原Task。历史archive、普通execution引用、ActivationBlocked和Unconfirmed均无权入场。原失败/原Committed历史不可改写。
2. **连续持有与入场**：先发布新owner/Task和新的admission owner；原execution从不Suspend/Dispose/reclaim。原controller专用serial下按setup→Host exclusive→upgrade→Attach原execution取得mutation admission。原未由controller Start的browser host靠exclusive Host fence等待/拒绝，不按PID收养或kill。原Ticket完整Flush/Close前保留setup。
3. **新持久计划**：随机新operation，known upgrade-pending.json写≤16KiB Ticket，绑定原operation/Prepared/Before/Committed摘要、原failedLaunch和execution记录摘要；独立≤128KiB计划记录精确restore/target及保留资源。partial/关闭未知保留pending和原对象，不归档。原Scope/Prepared只提供不可变原资格与lineage，不复活其mutation授权。
4. **复核和补偿**：在原registry owner重入下复核exact committed current、B owner/active、H receipt/Chrome/Edge和原历史，并重新验证新G/B及旧current完整v3、旧B/Core/revision和旧Host归属闭包。顺序H receipt→Edge default→Chrome default→B active→G current。每次先登记attempt、复核整个合法逆序前缀；observed foreign/原close未知永久锁存，不重新读洗白，不写更早依赖。普通write-after-effect错误也保原异常和pending，不冒充已完成。首次安装恢复current/active/receipt缺失，保留原归属B owner/bundle和物理空键，在终态列明，不泛化删除安装痕迹。
5. **取消和未知**：Closing前在各边界检查取消，停止新依赖写并保留原pending/execution及全部原操作；不重放/自动resume/cold fallback。Closing仅在全部目标已恢复复核且取消未赢时一次进入；Closing后只执行已授权原退役/结算，不再引入补偿写。失败结算逐原Task/registry/file/admission保错；single EDI/multiple Aggregate。Unconfirmed不提升成功，不凭新读清foreign。
6. **精确退役**：新增封闭的原execution retirement owner，Owned→Retiring→Retired。Closing时原lease仍held，原record精确Move到新Ticket绑定的retired路径；attempt前先将owner发布给新原Task，任何Move未知保留不重试。开始Retiring后永久拒原Attach.Require/mutation；不全局放宽Require。Closing只读复核使用原admission cleanup lease、Host fence及retirement owner的exact源缺失/目标原字节，不再调用原live execution.Require。pending全程阻断正常入口。
7. **唯一成功点**：预发布的原finalization Task拥有Closing所有新读取和terminal IO，自身不算已terminal集合。先复核恢复/retired/history，逐一join并关闭本轮新读取/registry/file，再关闭原admission、execution lease与controller专用serial。若关闭后再次复核，仍由同一预发布finalizer重新登记、逐Task join及逐handle close，不能沿用旧CleanupConfirmed快照。terminal写/Flush/Close、替换pending及exact检查也由该原任务的独立IO账本登记，含这些新增资源的全部关闭确认后，最后Move到compensation-finished-{newop}.json才Compensated。最后Move后无可失败IO/Dispose/serial释放。Move未知仅Unconfirmed历史，绝不重放。原controller在final Move前已确认退役并封闭，不能复用；原retirement对象仍由原补偿owner保留。Compensated仅三方恢复完成，不等于旧会话READY、真实Windows/完整安装P7或native/runtime验收。

最小源码范围：新Core补偿owner/计划/逆序前缀与finalizer；原Session一次签发/原controller专用serial与execution移交（不换对象）；原Execution封闭退役owner；借现有IO ledger publication channel及原registry snapshot的sticky冲突合同。正常启动路径、旧部署final Move、现有注册权限和旧current-only rollback不改义。

确定性验证：仅原RollbackRequired获权、同owner租约无释放窗口/Host shared拒绝、成功旧版本与首次安装restore、各逆序阶段错误/取消/foreign、旧G/B损坏零依赖写、registry冲突后外部恢复仍停、原write/close双错、Ticket/计划partial与关闭未知、held原任务、Retiring move/close未知、Closing后取消、last Move before/after-effect未知不重放、原owner退役后不能再mutation。仅必要相关集合，失败保原件不循环刷绿。

实现审查补充：Closing前在await原registry复核返回后、BeforeMutation回调后、temporary写入/exact核验后至实际Move前复核原取消；开始的原write仍由原Task结算，取消不等待写锁。退役仅Move及原exact核验正常返回后置MoveConfirmed。强CleanupConfirmed含原execution退休且lease关闭，不能与有意保留live lease混称。原Completion=Unconfirmed后可同owner纯关闭重试：先登记缓存原settlement Task，pending共用、成功且返回错误列表为空时继续共用；只有fault或非空错误列表才新一次原关闭尝试并保留旧Task。不得重读、新Move、重放prefix、释放live/Move未知execution、清pending或提升失败；Compensated路径仅返回内存空结果，不追加关闭IO。
