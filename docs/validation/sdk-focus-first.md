# Focus先评估、Hit祖先惰性生成：一次定点验证

基线 `00ccedd9780be322cebe2c2e958d3225ff4d1f26`。依监督授权，本轮实际小修复使一个调度器依次绑定原root/PID的focus祖先并评估；只有全部候选继续寻找才生成/评估hit祖先，最后一次BFS。成功和Sensitive/Stale/TooLarge等终态立即返回，不降级。共同64候选计数、原copy_ranges和原origin，不用两次搜索，不重试、预热、缓存或放宽250/600ms。每条最多8层、需抵达原root；保留最初focus/hit密码门、每候选密码门及最终来源校验。这里省掉的是hit祖先枚举；初始ElementFromPoint和hit密码检查仍先执行。

可选诊断默认关闭，只保存固定有限检查点、首次超时评估来源、最后批次来源、合法0—64范围总数/完成数和最后GetText的累计整数起止。评估外首次超时来源None，最后批次独立保留；首次检查点/来源不被后续检查覆盖。新增BeforeCopy只包住原有检查，不新增clock/check次数或拒绝分支。合法零范围为0/0，非法数量不截断为64。每批次仅清理诊断槽，实际额度/copy_ranges不重置；完成计数指范围文本复制/验证完成。

GetText结束时刻在HRESULT返回之后、check(hr)之前读取，仅最后一次调用，排除其后的BSTR复制/清零/释放。RangeText超时表示某次GetText已返回后检测越线，不能证明该调用是根因、返回正确文本或完整范围已处理；需结合RangeCompleted/RangeTotal区分未完成范围与空候选搜索。只记录固定数字/enum，不保留文本、路径、对象ID、指针或任意异常。原normal exit0、nonce/双帧/EOF、结果校验、600ms/取消/dispose发布门通过后才发布；Crash/Kill/协议失败/迟到不发布、不从marker补填。

本地实际验证：生产portable调度器回调26/26 Pass，覆盖focus成功不调hit祖先、无结果才调一次hit、终态不降级、hit先于唯一BFS、共用64预算及到期/异常；recorder36/36 Pass，含AfterCandidate/BeforeCopy首次记录后FinalEmpty不覆盖与合法0/0。g++C++20/Werror及ASAN/UBSAN通过，LSAN因ptrace限制Blocked。原/新Selection实际xUnit方法及断言与真实控制子进程160/160 Pass，无跳过或删除旧测试；VSTest本地socket限制Blocked，使用无端口runner。托管构建、probe实际源类型检查0warning/0error。PS固定摘要102/102和编译证据脱敏91/91 Pass，diff检查Pass；在线NuGet审计NotRun。这些不是Windows UIA提供者、安全桌面或物理划词通过证据。

精确冻结独审后只启动一个新进程的首个Chrome Html Word合成请求；现有read-only windows-2022、15min和诊断三个开关不变。没有Edge、其他Chrome视图、旧backend/六项/120/GetData或模型调用。不得自动重试或追加验证。WindowsSDK编译及定向取词结果待本次唯一run；以前两个5/6、四AV/HardTimeout和首次九段Correct0均保留，P0/真实密码/提权/不同HWND/物理跨应用/PDF/Markdown/多屏DPI/安装卸载仍未验收。GetData代码与Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 独立保留，不重跑。


## 唯一Windows定点结果：仍为0/1，进入祖先前超预算

代码与Actions SHA **`b6618b53579af403cf146c940e9e7e29b17caa35`**，tree `1c4177517e45f00a1168535e50343c999bb596fa`，唯一parent `00ccedd9780be322cebe2c2e958d3225ff4d1f26`；已独审本地冻结 `1a28c90fd6e28de89cef55bd7e30720f2be0df21` 同树。[run38404072468](https://github.com/T1doo/KuaiYi/actions/runs/38404072468)，job115318937348，attempt1，**success仅指有界诊断执行/清理，不是取词通过**。唯一一次，无预热/追加/重跑。

正常完整回执：**Observed / TimingDiagnosticOnly / SdkCom / Planned1 / Correct0 / ExitCode0**。Chrome154.0.8037.98 Html Word：**SoftTimeout / FocusAncestors / None / HRESULT null**，Mismatches0；SequenceCompleted/BrokerCleanup/ApplicationCleanup/ProfileDeleted/RunnerSettled全true。marker setup Ready，BrokerExitCode/ReachedBoundary均null，无Crash/HardTimeout。WindowsPS固定摘要102/102、编译证据脱敏91/91、WPF纯几何4/4 Pass；已越过SDK原生构建/预期exe启动门，原/WX保留，不宣称构建本身证明UIA正确。

| 阶段 | 起始累计ms | 结束累计ms | 差值ms |
|---|---:|---:|---:|
| Preflight | 0 | 0 | 0 |
| ComInitialize | 0 | 0 | 0 |
| AutomationCreate | 0 | 5 | 5 |
| Root | 5 | 229 | 224 |
| Focus | 229 | 272 | 43 |
| Hit | 272 | 294 | 22 |
| Password | 294 | 320 | 26 |
| Walker | 320 | 325 | 5 |
| FocusAncestors | NotRun | null | null |

整数毫秒截断，0不是绝对零。最后一段未执行，其他段连续Measured，没有OverLimit。**首次观测到累计跨250的区间为Focus229—272；最大差值为Root224。首次跨线不等于最大耗时/根因。** 原有调度器在焦点祖先阶段入口检测超时：FocusAncestors计时scope尚未进入，因此NotRun，并非祖先遍历耗时超限。

固定progress回执 **Checkpoint=FocusAncestors，TimeoutSource=None，BatchSource=None，CountState=Unknown，RangeTotal/RangeCompleted=null，TextState=NotRun，TextStartMs/TextEndMs/TextDurationMs=null**。没有候选进入评估、没有GetSelection批次/范围或GetText计时，不可解释为“范围未完成”或“空候选耗尽”；本次是候选搜索开始前原预算已耗尽。初始Hit和密码检查仍执行，不能宣称零hit provider调用。Hit祖先以及BFS未执行。本次未到达调整的候选评估路径，所以**不能据此判定focus优先修复已改善或无效，也不能宣称成功取词**。

结合此前9398edf首次九段Root105/Focus46、FocusAncestors175—184与RangeText超时，这两次请求展现不同位置的超预算。当前Root224/Focus43支持先调查原计时范围内初始化/root/focus可避免的提供者往返；单次不能证明固定架构成本、必须放宽期限或BuildCache能解决。没有在本轮移出计时、改变安全门、BuildCache、重复六项或任何额外CI。下一步需监督方按这份证据决定最小代码变化或测量；本轮授权的一次验证已用完。

安全回归补记：原候选顺序46/46、policy51/51以ASAN/UBSAN/Werror通过；并不取代真实Windows密码/提权/不同HWND安全实测。旧两个5/6、四AV/HardTimeout及上一首请求Correct0保留；生产默认关闭，ModelCalls0/ClipboardFallback Disabled，真实物理划词、Win11、PDF、Markdown、多屏DPI、安装卸载、P0仍NotRun/Blocked。

公共job日志只留本地忽略文件 `artifacts/windows-selection/windows-focus-38404072468.local.log`：**20571 bytes**，SHA256 **`cd5dd74d21d953f1df2ff7e1fa6525e1da9f4d173738c0364992b6509ecbe2c7`**，不上传原始stdio/receipt。GetData代码与Actions SHA **`220060f813a3640487d593920a1b1824aa779a00`**独立保留、未重跑。
