# P6-02：PNG eXIf 的受界 IFD0 方向支持

2026-10-20，从已独审、clean、远端一致的 `0dfb16f37f65be61a1cd08502ddf62b640f7f0b4` 接续，单worktree、唯一root writer，正常分支 `dev/p6-png-exif`。对应原SPEC7.3/P6-02；只交付受界PNG eXIf方向子集，不代表完整PNG/Exif、多格式或真实画质验收完成。

## 接受与拒绝合同

[W3C PNG3 的 eXIf 和chunk顺序规定](https://www.w3.org/TR/png-3/#eXIf)要求单个eXIf在首IDAT前，内容从TIFF头开始；它也提醒Exif可能是历史元数据，规范不负责解决冲突。本产品按原SPEC显式应用正式eXIf方向，但不声称能证明其历史真实性。

父端扫描结构及受界eXIf：PNG签名/尺寸、首个且唯一IHDR、首IDAT前最多一个eXIf、连续IDAT、唯一末尾空IEND、chunk类型字节和长度界。最多256chunk，沿原prepared上限；现在Worker的raw/prepared也独立执行此受界检查。eXIf最多65,527字节，是产品子集上限，不是PNG规范上限。父端核eXIf本身CRC，不扫描大IDAT的全部CRC；Worker独立核原文件全部chunk CRC，之后才允许处理native输入。acTL/fcTL/fdAT均明确Unsupported，未支持动画首帧。

eXIf为裸TIFF，拒JPEG Exif前缀和BigTIFF。支持II/MM、magic42、合法对齐首IFD偏移及最多256项的完整IFD0（含nextIFD字段）。Orientation只能SHORT、count1、值1–8且最多一次；没有Orientation时为identity。存在的IFD0 Width/Length须为单值SHORT/LONG、不得重复，并与旋转前IHDR尺寸精确相同。

PNG未知tag只接受类型1–12，非零count及checked 64bit长度/偏移运算；out-of-line范围不得越界或覆盖TIFF头/完整IFD0。允许不同未知tag共享同一合法数据区，不解释这些opaque内容。SubIFD/Exif/GPS/Interop指针tag、任何非零nextIFD（包括自环和越界链接）直接Unsupported，不遍历链；这不是完整TIFF字段或嵌套元数据解析。重复/冲突方向、坏CRC、非法类型/值/尺寸/布局等仍明确拒绝，不猜测或偷偷按identity发送。

原JPEG APP1/IFD0子集语义保持：新增PNG指针/nextIFD/尺寸规则不会套到JPEG，旧JPEG拒绝与native方向一致性门保持。原20MiB/24MP/单边16384、帧/消息界、白底/面积缩小、4MiB请求编码及全部取消/清理/实际退出门不扩容。

## 固定 native 的真实能力与输入脱敏

仅放开解析后，同一方向6合成PNG仍在实际child返回Unsupported。一次独立合成native试验定位当前固定Linux Skia4.153.1报告Success/Png/origin1/frames0/3×2，GetPixels返回完整未旋转原画布：它忽略该eXIf。探针stdout及精确像素断言保留；最终探针还对当前Linux明确断言TopLeft。不把它写成native已支持方向6或Windows结果。

调整设计独审后，Worker先检查原job、长度/SHA、完整CRC和严格TIFF；raw非identity仍在native构造前Unsupported。对可接受PNG，直接分配 `原长度−已验证eXIf完整chunk长度` 的唯一native copy，分两段逐64KiB复制，只移除该chunk。没有先ToArray再做第二份全图，也没有重试更宽松的解码。无eXIf时copy保持原字节。

native只收到此copy，并须精确报告TopLeft；原 `sourceOrientation` 与 `nativeExpectedOrientation` 分开命名。原方向仅交既有area内核应用一次，原job及回执source尺寸/方向始终绑定未删改输入。IHDR/IDAT及其他chunk逐字节保持，native仍须满足格式/原画布尺寸/帧数/GetPixels Success等检查。PNG输出继续仅含IHDR/IDAT/IEND，不复制原Exif或路径；encoded仍无桌面锚点。

helper异常/取消清理实际filled copy，Worker在成功、native失败及取消的finally清理copy和原始像素。受控忽略CT的原decoder任务真实结算后，session清理晚栅格；实际child完整栅格后挂起再取消、额外输出、错误source metadata和exit7也不能交付PNG。框架/native内部内存清零未得到证明。

## 实际验证

| 最终本批运行 | 结果 |
| --- | --- |
| Codec Release构建测试 | 214P/0F/0skip = 新85 + 原129组 |
| Provider传输限定filter | 37P/0F/0skip，非全Provider |
| Worker/App Release交叉构建 | 各0警告0错误；不是Windows运行 |
| Worker/Codec/Provider/App locked restore | 四个exit0，audit all/low、warnings-as-errors；锁和依赖版本不改 |

II/MM×八方向经真实生产源码fixture child，3×2不对称六灰度用独立硬编码顺序逐像素核只旋转一次，实际退出后交付；方向6完整请求PNG、数值映射/原job/原输入不变亦核。缺Orientation、偏移12、声明尺寸匹配、未知tag共享合法数据区有正例。重复或后置eXIf、断续IDAT、CRC/长度、36种TIFF不良或不支持profile、APNG、raw非identity等保拒绝；原始长度/摘要错配及非eXIf的IHDR/IDAT/tEXt CRC错误不能因移除步骤被隐藏。65,527/256项/256chunk精确边界测试与越界拒绝保留。

直接链接同一生产decoder/扫描器到既有codec测试工程，沿用已批准固定包，不引入新依赖。逐字节核只移除eXIf；实际native copy在成功、压缩像素失败、observer异常、取消及受控晚返结算后清零。此处的直接decoder/原task端口不是独立进程内存窥读或Windows验收；跨进程用例核真实返回像素、接收buffer清零及实际退出。

基线同一功能方法实际1F（父端旧eXIf拒绝、尚未启动child）；严格parser候选同方法又1F（实际child的native方向不匹配）；探针1P明确定位忽略行为。输入脱敏后新组首76P，最终扩大到85且整组214P；中间不累计。原129组中仅一条旧“所有eXIf拒绝”合同改为“nextIFD自环仍拒绝”，保持Unsupported/child starts0等原断言；其余128案例相关源码与helpers不改。没有删测试或把这些失败说成native bug已修复。

最小设计、native脱敏调整及最终源码/回执独立只读审查无剩余可确认P1/P2，测试由writer执行。71项基线保护核对全真，原SPEC/产品书、依赖锁/许可证/原生代码/启动器/工作流、已有截图路由/映射/两个Coordinator等保持；App/Imaging实际assets/output仍无Skia，Worker声明副本原字节相同。见[原件与摘要索引](../test-evidence/2026-10-20-png-exif.json)。

## 剩余条件

本IFD0子集与合成回路范围Pass；完整Exif/嵌套链/动画、完整JPEG及BMP/WebP/GIF、打开/投放UI、本地字号预检/真实画质和Windows截图/native/Worker/OCR/WPF仍未验收。下一独立候选是按原格式清单设计受界BMP接入，不以本轮子集称全格式完成。

250/600ms、来源/密码/权限/完整性/资源/取消/清理/隔离门、PackageHold和生产图片复用Off保持。无真实图片/截图/私人文件/模型/新依赖许可或安全权限/CI调度/费用/凭据/发布/用户电脑操作；main不改，旧CI39347646426 step3被拒日志未请求或绕路。公共规范阅读及当前固定库合成试验不扩大既有授权，Linux证据不替代Windows实机。
