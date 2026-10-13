# Focus先评估、Hit祖先惰性生成：一次定点验证

基线 `00ccedd9780be322cebe2c2e958d3225ff4d1f26`。依监督授权，本轮实际小修复使一个调度器依次绑定原root/PID的focus祖先并评估；只有全部候选继续寻找才生成/评估hit祖先，最后一次BFS。成功和Sensitive/Stale/TooLarge等终态立即返回，不降级。共同64候选计数、原copy_ranges和原origin，不用两次搜索，不重试、预热、缓存或放宽250/600ms。每条最多8层、需抵达原root；保留最初focus/hit密码门、每候选密码门及最终来源校验。这里省掉的是hit祖先枚举；初始ElementFromPoint和hit密码检查仍先执行。

可选诊断默认关闭，只保存固定有限检查点、首次超时评估来源、最后批次来源、合法0—64范围总数/完成数和最后GetText的累计整数起止。评估外首次超时来源None，最后批次独立保留；首次检查点/来源不被后续检查覆盖。新增BeforeCopy只包住原有检查，不新增clock/check次数或拒绝分支。合法零范围为0/0，非法数量不截断为64。每批次仅清理诊断槽，实际额度/copy_ranges不重置；完成计数指范围文本复制/验证完成。

GetText结束时刻在HRESULT返回之后、check(hr)之前读取，仅最后一次调用，排除其后的BSTR复制/清零/释放。RangeText超时表示某次GetText已返回后检测越线，不能证明该调用是根因、返回正确文本或完整范围已处理；需结合RangeCompleted/RangeTotal区分未完成范围与空候选搜索。只记录固定数字/enum，不保留文本、路径、对象ID、指针或任意异常。原normal exit0、nonce/双帧/EOF、结果校验、600ms/取消/dispose发布门通过后才发布；Crash/Kill/协议失败/迟到不发布、不从marker补填。

本地实际验证：生产portable调度器回调26/26 Pass，覆盖focus成功不调hit祖先、无结果才调一次hit、终态不降级、hit先于唯一BFS、共用64预算及到期/异常；recorder36/36 Pass，含AfterCandidate/BeforeCopy首次记录后FinalEmpty不覆盖与合法0/0。g++C++20/Werror及ASAN/UBSAN通过，LSAN因ptrace限制Blocked。原/新Selection实际xUnit方法及断言与真实控制子进程160/160 Pass，无跳过或删除旧测试；VSTest本地socket限制Blocked，使用无端口runner。托管构建、probe实际源类型检查0warning/0error。PS固定摘要102/102和编译证据脱敏91/91 Pass，diff检查Pass；在线NuGet审计NotRun。这些不是Windows UIA提供者、安全桌面或物理划词通过证据。

精确冻结独审后只启动一个新进程的首个Chrome Html Word合成请求；现有read-only windows-2022、15min和诊断三个开关不变。没有Edge、其他Chrome视图、旧backend/六项/120/GetData或模型调用。不得自动重试或追加验证。WindowsSDK编译及定向取词结果待本次唯一run；以前两个5/6、四AV/HardTimeout和首次九段Correct0均保留，P0/真实密码/提权/不同HWND/物理跨应用/PDF/Markdown/多屏DPI/安装卸载仍未验收。GetData代码与Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 独立保留，不重跑。
