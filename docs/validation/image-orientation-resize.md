# P6-02：受界 EXIF 方向、一般缩小与白底合成组件

2026-10-20，从已独审、远端一致且 clean 的 `110afa78aefaf0b49443887e2cb0b6c673084984` 接续；单 worktree、唯一 root writer，正常分支 `dev/p6-image-orientation-resize`。对应原 SPEC7.3/P6-02，在上一批真实隔离解码之后实现预处理，不接真实文件或投放入口。main 保持 `51d077481cf6303602fcf5d2718213da9cf05178`。

## 本批实际交付

`ImageWorkerClient.PrepareAsync` 只接受 raw source job，再把最大长边作为不可变 wire job 字段绑定；默认2560、允许更严格1–2560，不放大。原 `DecodeAsync` 的 raw行为保持，可选字段为null时不写入JSON。仍用同一107–109私有通道、一次性Worker和旧完整终止门，不增命令网关入口或UI。

JPEG沿用有限baseline格式，新增 IFD0 Orientation1–8的方向校正；只读APP1/IFD0所需数值，校验byteorder、magic、相对偏移、项数、类型/计数、值及重复块/tag，不跟GPS、缩略图或nextIFD。最多256个JPEG头segment、256个IFD项；超限明确Unsupported，不分配输出。PNG仅支持identity预处理，并主动拒绝所有eXIf，即便原生codec忽略该chunk也不能假称已应用方向；PNG头部遍历同样最多256个chunk。其他原格式限制保持，未称全格式支持。

`ImageTransform.PlanEncoded` 记录原始尺寸、方向修正后尺寸、实际舍入后的输出尺寸和方向。连续pixel-edge坐标的点/矩形及逆映射处理旋转/反射；缩放轴以oriented尺寸为分母，短边四舍五入且至少1像素，`Downscaled`只表示缩小。encoded没有桌面锚点，OriginalCrop与所有Desktop映射拒绝；旧截图 `Plan(PixelRect)` 及其严格边界/roundoff行为保持。record安全ToString只读尺寸、方向和锚点状态，避免自动打印触发拒绝属性。

Worker在完整原生GetPixels Success后，直接按方向逆索引对源栅格做box/area缩小和主题无关白底合成，不先生成另一张完整旋转图。整数面积权重覆盖完整source，64bit累计`channel*alpha + white`，最后仅舍入一次；输出紧密top-down `BGRA8888-opaque-white`、实际alpha255。它是存储通道值的面积平均，未宣称文字可读性或所有画质评估已过。

父端独立读取原尺寸/EXIF，按输入摘要、精确job/源方向/共享plan核验预期格式、输出尺寸/stride/总长，再分配。raw/prepared元数据互斥；失败不能携带栅格/方向元数据。接收端除检查块序、唯一commit、摘要，还检查实际每个alpha为255；额外输出、非零退出、未确认IO/退出、取消或晚回执均不能交付，实际buffer清零。原operation/control/tail任务和owner持有规则保持，Worker原始像素/编码副本及失败destination的finally清零均保留。

## 资源、依赖与隔离

输入仍20MiB、24MP、单边16384，64KiB JSON/1MiB binary、15s及原进程终结门不变。单份raw像素最多96,000,000字节；prepared destination不大于原像素数且最多26,214,400字节，没有第二张完整旋转图。逐输出行及每4096个source贡献检查取消。上述是受界数组尺寸，不是总Private Bytes峰值、原生内部内存清零、600MiB或250/600ms性能实测。

沿用获批四个SkiaSharp4.153.1包，所有版本、依赖锁、原许可证/完整声明和生产Worker输出声明均未改。此轮仅补读同固定Skia gitlink的[SkEncodedOrigin.h](https://github.com/mono/skia/blob/45afab4f1f0921f3feb97f58cf89b136fffa85e6/include/codec/SkEncodedOrigin.h)及固定托管Definitions blob，官方API各一次HTTP200，正文重算Git blob SHA1匹配，原件封存；不是重新安装依赖或扩大供应链结论。App/Imaging实际assets libraries及输出仍无Skia，Worker许可证副本仍与原字节相同。[上一批供应链及解码边界](isolated-raster-decode.md)继续适用。

## 实际验证与因果范围

| 本轮最终运行 | 结果 |
| --- | --- |
| codec Release构建测试 | **103P/0F/0skip = 新54 + 原49**，原49测试源与110afa逐字节相同 |
| 最终新预处理原产物no-build/no-restore复跑 | **54P/0F**，与上行重叠，不累加 |
| 原Core相关限定filter | **74P**：Worker/startup/finalization/IPC，非全Core |
| 原Provider相关限定filter | **154P**：传输、截图归一化和坐标边界，非全Provider |
| Worker / App Release交叉构建 | 各**0警告0错误**，App八个相关项目；不是Windows实际运行 |
| Worker/codec/Core/Provider/App locked restore | 五个全部exit0，audit all/low + warnings-as-errors，依赖锁不改 |

8种JPEG EXIF方向用真实子进程、3×2不对称像素和独立硬编码顺序逐像素对照，并交替大小端；坐标正反向/矩形/严格越界、非整数缩放、无桌面锚点亦覆盖。5×3→3×2独立预期23/103/183/37/117/197，棋盘和最后一列细线检查抗漏边/混叠；纯生产preparer端口的低alpha样本证明一次舍入254而逐像素先舍入会得253。实际PNG路径另测透明白底。受控端口仅验证原task晚返/临时buffer所有权，不冒称native实机时限。

真实3840×2160非恒定白边PNG，经生产源码fixture子进程生成2560×1440、14,745,600字节的多块栅格并实际退出；独立检查左上/右下角227、顶边相邻点170与中心0这些代表像素，并非独立逐像素验证大图所有边/全部内容。该用例不是内存峰值或平台性能验收。另测错误sourceInfo分配0、带新摘要的假opaque像素、额外字节/第二commit/exit7、完整栅格后挂起再取消的实际清零和退出。

保存110afa实际Linux生产源码fixture产物8文件的摘要后，9个相同有效方法对旧child均失败于Prepare新选项，原raw基线仍可读；新child对应方法全过。这是旧版本不支持新协议选项/功能的对照，**不是9个旧错误方向或错误缩图缺陷**。两方法正文及硬编码Order表与该运行快照相同。首次测试编译xUnit2014/CS0619为0执行，原源码/日志保留；中间新49/53P及安全ToString前的一组绿回执亦保全，不与最终计数相加。安全ToString是审查发现的潜在record属性问题，未把它说成已执行红例。覆盖写TRX的logger提示对应旧文件已事先完整归档，不是编译警告或结果丢失。

最小设计和最终源码/回执均经过独立只读审查，无剩余可确认P1/P2；测试由writer执行，reviewer没有运行构建/测试。原件、source绑定和命令见[证据索引](../test-evidence/2026-10-20-image-orientation-resize.json)。

## 剩余条件

本组件受界范围Pass；完整P6-02/P6-06仍InProgress。PNG eXIf、全JPEG/其他完整格式、4MiB编码与严格API限制、文字可读性评估、生产截图一般缩小接入及打开/投放UI尚未完成。现有截图归一化入口仍保留其原常量图缩小限制；新一般缩小在Worker预处理接口中，未暗中切换生产截图路由。Windows真实Worker/native加载/OCR/WPF、整P6和发布验收仍NotRun；交叉构建及Linux fixture不替代这些证据。

来源/密码/完整性/取消/清理/隔离门、250/600ms、PackageHold及生产图片复用Off保持。原SPEC/原生代码/SDK启动器/工作流和所有旧证据不改，无真实用户文件、投放、模型、CI调度、费用、凭据/安全权限、main修改、强推、部署、发布或用户电脑操作；旧CI39347646426 step3拒绝日志没有查询或绕路。
