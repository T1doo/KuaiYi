# P7-01 B：正常安装会话的单图转交

状态：限定实现与Linux固定树完整签收Pass，独立源码/原件限定ACK。P7-01仍为InProgress，真实Windows身份/ACL/菜单→同WPF→Worker链尚未验。

从`74aed50086d6af063deb91143938e993b3615970`接续，指定环境唯一writer，`dev/p7-fixed-signoff`，原远端一致；不改main。沿已独审[原设计](../design/p7-warm-image-admission.md)实施，仅单个本地文件预览，DefaultOff、不自动OCR、启用或发送。

生产调用链：`Program --prepare-image` → Windows管道双向真实peer → 原normal controller的`LiveImageSession` → 同原stdio的独立warm actor → UI完整stamp → `StartInstalledImage/StartImagePreparation` → 原安全打开/Worker实际finalize → `AcceptFileImage`的warm分支 → `WarmSync→Gate`纯内存事务 → 出锁后精确旧资源清理/图片专用WPF呈现 → `ConfirmPreview` → 同request/真实SourceVersion GUID结果。指纹、Show、锁冲突、synthetic token不授予此权限。

- 真实pipe PID/session绑定到held query/synchronize进程handle、创建时间、选中generation确切映像、同user/logon/AuthenticationId/session、medium8192且非elevated/AppContainer/UIAccess；关键阶段重复检查。没有kill/adopt peer。
- 本地first-instance原server handle单实例复用；显式logon SID DACL和client rights均去掉`FILE_CREATE_PIPE_INSTANCE/APPEND_DATA`，client SQOS identification。生产没有synthetic身份开关或新依赖。
- 原健康normal Role、Start返回的exact child、原admission/execution租约与原proof构成不可由wire claims创建的live capability。endpoint只占一个原Role复合Task，不等待贯穿session的serial锁。
- 仅精确NoEndpoint(Win32 2)允许尝试原cold入口，且仍须实际取得原租约。busy、认证拒绝、超时、丢收据或断线不重放、不自动cold fallback。冷IMAGE/OFFER一次性门不放宽。
- Header后、payload分配前限定阶段类型；严格受界规范JSON。一个slot、零队列、连接/nonce/request/ordinal绑定，10秒grant/15秒prepare沿原TimeProvider门。
- UI reserve只有健康/非busy/非锁屏/无设置模态时冻结原Epoch/Activation/Input/Result/Settings。迟到hash/Dispatcher/Worker返回仍核原stamp/current permit。原Worker finalize先于任何来源发布。
- `AcceptFileImage` warm分支同时提交真实LocalImageVersion GUID与AppOwned；Gate只执行原EndCapture+StopGeneration的两次epoch推进、分离原CTS及管理内存交换。锁内无Cancel/Dispose/WPF/磁盘/Dispatcher等待；所有current回调不反取WarmSync。
- 图片专用呈现避开缓存selection的general Markdown Apply，逐边界复核来源/version/epoch/activation/result/imageSelected。旧region/document/block精确清理；一个有界retired batch保留擦除失败owner，另一个staged region不冒充已呈现。失败项可原对象重试，重入的新source不被旧清理覆盖。
- AppOwned不等于PreviewReady；只有实际同candidate预览确认后才记后者。post-cut换来源可保留真实所有权却不报旧预览成功。accept/render/包/协议/退出错误保留原fault/清理门；仅accept未开始且原清理全部确认的InvalidImage/ImageTooLarge/UnsupportedImage输入拒绝可退回Rejected。正文与清理双错保全。
- 连接拒绝与原结算故障分轨，先join原verify/read/write/cancel/Result/Finish；普通EOF或坏phase完全结算后可用同原server handle接下一次显式请求。原private写、Worker或cleanup未确认仍占slot并拒绝替换。
- 原普通角色即使无parent Warm，只消费精确proof绑定的WARM_QUIESCE撤销通知，记录已撤销并禁止迟到AttachWarm；其他回执仍拒绝。合法handoff仅可封闭从未使用且无待结算任务的idle actor，不向已关闭的原runtime通道补写通知；已使用warm仍永久拒OFFER。

在clean固定实现`92e3548845bd07ee7717b53df9588c412d128f9d`各执行一次：

| 验证 | 实际结果 | 全命令耗时 |
| --- | --- | --- |
| 完整Core b13 | 2164P / 0F / 0skip | 97.198s |
| 完整Provider b14 | 2463P / 0F / 0skip | 38.103s |
| 完整Codec b15 | 481P / 0F / 0skip | 35.577s |
| 最终App Windows目标交叉构建 b16 | 0 warnings / 0 errors | 9.318s |

四轮前后HEAD相同、dirty0，836输入逐项相同且等于git固定字节。旧Core2140全保留+24、Provider2449全保留+14、Codec481全同；不累计不同轮次数。每轮330项DLL摘要变化分别45/10/8/13，表示正常构建差异，不是全部DLL执行或全部前后一致。当轮目标AssemblyInfo绑定92e；全目录额外采集的未参与当轮构建项目仍可能保留旧版本，不泛称全部generated均92e。

[完整命令/日志/TRX/逐字失败XML/前后DLL与836源码输入/原件ZIP索引](../test-evidence/2026-10-21-warm-image-b.json)。原件ZIP 7,577,354字节，SHA256 `ddc4dcf65f5cf4759766343d2d1cf15fbfd73e871cfb672670a22f41ccb0d68e`，1668成员；MANIFEST列出的1667项字节数/摘要与CRC均核验。Library上传已成功，ID `libfile_11ab83f8701c8191bca25470a98977e9`、version0；不分享私有下载URL。

开发过程中保留失败轮：编译链接/未await告警、旧位图owner丢失反例、新夹具空settings stamp差异、包校验精确异常类型错误、已释放Process上的诊断查询错误；不将失败轮回填为通过。不同轮次不累计。

运行边界：本片端点初始化、native bind、自身身份/证明异常仍保守fault并保留正常session原fence；没有新增“未连接时自动降级/重绑”恢复路径。真实Windows API/ACL/身份、Explorer实际来源、OLE/COM/WPF/OCR、画质、250/600ms、600MiB及完整P6/P7/106/Beta均未验。已完成handoff且无原normal controller的runtime不开放warm入口。PackageHold、生产复用Off、静态WebP奇数payload明确拒绝、原所有权/取消/清理/发送门保持。
