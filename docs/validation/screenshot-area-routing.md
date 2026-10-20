# P6-02：生产截图一般面积缩小、PNG及物理坐标适配

2026-10-20，从已独审、clean、远端一致的 `866c86a75beee13044b56fcd7377a822d7d1735e` 接续，单worktree、唯一root writer，正常分支 `dev/p6-screenshot-area-routing`。对应原SPEC7.3/P6-02下一核心缺项；本切片实现生产截图已有final-crop准备接口的一般缩小接入，验证既有Unreadable拒绝门。不是新增本地字号检测或真实画质验收。

## 实际接入

生产 `ProductionScreenshotCoordinator` 原本通过同一个Task.Run调用 `OwnedSourceImage.PrepareRequest`，原来源/设置/request/epoch/绑定、用户确认、独立阶段secret lease、统一预算及每次HTTP发送门均不改。本次在该准备接口内，把“非恒定超长边一律拒绝”改为共享面积缩小；没有增加新发送入口。截图已经是最终裁剪的owned直通BGRA，不需要先编码再交给native解码；文件输入仍走原一次性Worker完整EOF/真实exit0/IO结算门，二者不混同。

`AreaWhiteRaster` 从既有Worker提取相同的整数面积权重、64bit累加、主题无关白底及末端一次舍入算法，借用readonly源，仅转移一个新的owned目的数组。每输出行及每4096个source贡献检查CT，异常/取消清理目的数组。Worker继续包装原prepared元数据/摘要；仅给两个既有生产Worker/合成fixture程序集内部编译可见性，不新增协议、进程、依赖包或运行安全权限。

截图保留原尺寸白底路径及精确扫描后的常量图fastpath，原两个proof含义不改。只有非恒定且需要缩小的图走新内核，记 `AreaWhiteCompositeDownscale`；它描述处理方式，不表示内容可读。旧 `CreateRequestPng` 原尺寸导出仍拒绝超长边，只有已有生产准备接口放入一般缩小。原所有来源/最大24MP/单边16384/编码限制保持。

桌面适配使用原 `ImageTransform.Plan(crop)`，完整ImageTransform类字节未改：真实物理负原点、原尺寸、实际舍入后normalized尺寸及各轴比例从最终裁剪保留到Prepared、Classifying、Generating和结果metadata。一般缩小得到的PNG尺寸必须与此plan一致；PreparedScreenshot构造还拒绝空source、非桌面锚点、非截图方向、错误尺寸及与是否缩小矛盾的proof，并清理已转入的PNG。编码文件自身不带坐标或路径，已有encoded文件接口仍不能凭空获得桌面锚点。

沿用单次lossless RGB PNG及总字节cap，默认4MiB、更严格64KiB..4MiB。不会为了成功再次改尺寸、降低质量或省略局部；超限仍在secret和HTTP前返回截取局部提示。原来源在锁内作受界独立copy后释放锁；处理持有自己的copy/raster/row/编码buffer，Clear可释放原owner。取消或失败的实际数组清理，最终PNG仅一次转移并在Dispose清理。原Coordinator始终await同一准备Task，清理和晚返门未改。ZLib/系统/native内部内存不在清零实证内。

## 可读性拒绝的准确范围

缩小结果若由既有分类阶段返回 `Unreadable`，显示“图中文字无法辨识，请重新截取更清晰或更小范围”，不进入正式生成、不打开第二阶段secret、不作第二次发送，保留已有完整结果。分类本身已是一阶段调用；不能说该场景完全没有发送。两协议均用本地合成fixture验证这一实际生产coordinator路径。

本地OCR字号证据、预处理后明显过小文字的发送前检测，以及真实文字/混合图画质评审仍未实现。没有发明像素尺寸或对比阈值宣称可读性已证明；此子项不使完整SPEC7.3或P6通过。`partly_unclear`等原分类语义未改。

## 实际结果和因果证据

| 本轮最终运行 | 结果 |
| --- | --- |
| Provider受影响限定filter | 372P/0F/0skip：一般截图、生产截图、原归一化/坐标/离线路由/owned来源，非全Provider |
| 原Codec完整组 | 129P/0F/0skip；原49解码、54方向/面积、26请求PNG测试源字节不改 |
| Worker/App Release交叉构建 | 各0警告0错误，非Windows实际运行 |
| Worker/Codec/Provider/App locked restore | 四个exit0；audit all/low、warnings-as-errors，锁文件不改 |

同一新测试方法在866c86a原准备实现实际1F，原因是非恒定图在编码前被拒绝；最终同方法通过。3840×6非恒定3×3重复瓦片→2560×4，独立预期每块20/87/33/100，逐RGB像素核完整输出，含最末边；真实物理crop为(-3841,50,-1,56)，生产两阶段实际HTTP body取出同一个PNG并核map、四边及相邻越界。3001×17和17×3001舍入到2560×15/15×2560，用独立decimal源坐标footprint逐像素核最后行/列白线贡献。6个原one-unit用例改为独立decimal面积量化逐像素预期，承认合法舍入可能把微小差异归零，不能保证每一原始像素差异缩后可见。

真实copy、目的栅格、row、容量和最终PNG数组分别保留观察：正常/Dispose、copy/raster/row/final时取消或抛错、严格API超限均核清零；原source在独立目的分配后Dispose亦可完成准备，证明锁外copy不借原buffer。生产Prepared回调Stop/Clear/换源/Off/锁屏阻止第一secret和分类。原4MiB拒绝测试改用真实1280×1280高熵源保留0secret/0HTTP及原源不变断言；另测一般缩小后仍超64KiB时不再次缩尺寸。

首候选有一处新测试误把CachedOverlayVersion与字符串比较，CS1503、0执行，源码和日志保留。首执行351P/3F：两Unreadable新测试误取SelectedSnapshot（产品保留旧complete选择），改为按新request identity取SnapshotForVersion，旧结果保留断言不放松；一个遗漏旧“2561非恒定应拒”测试等待HTTP超时，改为核新完整两阶段/实际2560PNG/源不变行为。同三个因果复跑3P，最终372P；中间计数不累计。最小设计及最终源码/回执独立只读审查，无剩余可确认P1/P2，测试由writer执行。见[原件与摘要索引](../test-evidence/2026-10-20-screenshot-area-routing.json)。

## 剩余条件

生产截图一般缩小的源码路由及合成回路在上述范围Pass；本地字号/质量预检和实测、PNG eXIf/完整格式、打开/投放UI及Windows真实截图/native/Worker/OCR/WPF仍未完成。下一可独立核心项为PNG eXIf方向严格支持；真实可读性依赖后续OCR字号/冻结样本和Windows证据，不伪造。

69项基线保护核对含原SPEC/产品书、原129测试、两个Coordinator、完整ImageTransform、原客户端/编码器、所有依赖锁/固定许可证/原生代码/SDK启动器/工作流均未改。沿用获批四包4.153.1；App/Imaging实际assets/output无Skia，Worker原许可证副本字节不变。没有新SDK/外部源码下载/CI调度/模型/真实截图或私人文件/费用/凭据安全权限/用户电脑/发布。250/600ms及资源门不变，未测Windows600MiB或性能；PackageHold和生产图片复用Off保持，main未改，旧CI39347646426被拒日志没有请求或绕路。
