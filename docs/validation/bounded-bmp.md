# P6-02：原格式清单中的受界 BMP 接入

2026-10-20，从已独审、clean、远端一致的 `2835f12887283b42cbecd677e5270d766286235c` 接续，单worktree、唯一root writer，正常分支 `dev/p6-bounded-bmp`。原SPEC7.1明确要求PNG、JPEG、WebP、BMP及GIF首帧；本轮接BMP，不新增格式范围。

## BMP 接受与拒绝合同

依据[Microsoft文件头](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapfileheader)和[信息头](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-bitmapinfoheader)的字段、行序和stride定义，产品当前只接受：BM文件头14字节、BITMAPINFOHEADER40字节、planes1、24bpp、BI_RGB无压缩、无调色板、像素offset54。正高度为bottom-up、负高度为top-down；native解码后两者均是逻辑TopLeft，不再施加EXIF翻转。

父端签名/Inspect及Worker native copy前都检查：实际文件不超过20MiB；宽正、高非0且非int.MinValue；原单边16384/24MP限保持；reserved字段0、bfSize等于实际长度、stride按DWORD行对齐、checked64位计算stride/绝对高度/payload/54+payload。实际总长必须精确等于此总长，无gap、额外尾部、截断末行或额外palette；biSizeImage允许0或精确payload。padding字节可非零，但不进入输出像素。

其他DIB头（CORE/V4/V5等）、1/8/16/32bpp、RLE/bitfields/嵌入JPEG或PNG、非零ClrUsed/ClrImportant（含极大声明）、合法但本子集不支持的gap均明确Unsupported；不把所有合法BMP变体说成非法。矛盾文件大小/像素偏移/planes/总长/SizeImage及非法有符号尺寸为Invalid，超过原尺寸或文件界为ImageTooLarge。只读固定头部数值，不按声明分配palette、跳读任意偏移或猜测32位alpha。

`EncodedRasterFormat.Bmp` 追加在旧枚举末尾，原值不改。Worker显式分派BMP，不能落入“非PNG即JPEG”；native必须实际报告Bmp、原画布尺寸、TopLeft、静态帧数0或1及GetPixels Success。Worker在raw/prepared分流前核实际每个alpha255并检查取消，父commit对BMP raw也核alpha；匹配新摘要的伪造非透明栅格不能交付。

raw输出紧密top-down BGRA，prepared沿用既有area/白底及无桌面锚点映射，PreparePng沿用单次metadata-free RGB PNG/默认4MiB及更严格API界。原job/SHA绑定、source metadata、owned清零、取消晚返、EOF/真实exit0/原IO结算、来源确认及发送门均保留。未修改截图路由、复用Off或PackageHold。

## 实际验证

| 本轮最终运行 | 结果 |
| --- | --- |
| Codec Release构建测试 | 286P/0F/0skip = 新BMP72 + 原214，旧四文件原字节不改 |
| Provider传输限定filter | 37P/0F/0skip，非全Provider |
| Worker/App Release交叉构建 | 各0警告0错误，非Windows实际运行 |
| Worker/Codec/Provider/App locked restore | 四个exit0，audit all/low、warnings-as-errors；依赖及锁不改 |

同一新功能方法在旧2835f128实际1F（父端Unsupported），接入后实际child生成3×2 PNG，六个不同B/G/R独立预期逐值核完整画布和行序；方法正文保留。宽1–4×两种高度符号×SizeImage0/精确共16种布局实际child逐像素核，padding固定非零；5×3→3×2两种行序独立预期23/103/183/37/117/197，未二次翻转。原单边16384两轴实际返回完整受界raw栅格。

39种拒绝用例覆盖头变体、压缩/位深/调色板、伪bfSize/offset/SizeImage、截断/尾部、int.Min/高位数值/尺寸界，均在child/native copy前拒绝。原job错长度/摘要、错source、额外输出及exit7不能交付；真实child完整栅格后取消核实际buffer清零及退出。直接生产decoder实际copy在正常、取消、observer异常及受控copy前两字节扰动引发真实native拒绝时清零，原input不变；后者是测试端口故障注入，不冒称某个通过preflight的普通BMP天然损坏。受控忽略CT的原decoder任务真实结算后，late raster/copy也清零，不当Windows硬时限实证。

首次小功能1P、首BMP组71P、补palette极大值/一致1×1 alpha攻击后最终72P与整组286P；中间回执不累计。最小设计及最终源码/回执独立只读审查无剩余可确认P1/P2，测试writer执行。原件、source/产物摘要和命令见[证据索引](../test-evidence/2026-10-20-bounded-bmp.json)。

## 原五种格式的精确当前边界

下表仅指受界隔离 `DecodeAsync / PrepareAsync / PreparePngAsync` 接口；入口能接收文件或后缀在名单中，不等于完整解码、打开/投放UI或Windows产品验收。

| 原SPEC格式 | 已接入范围 | 明确未支持/未验范围 |
| --- | --- | --- |
| PNG | 静态PNG受界容器/CRC和native完整像素；至多256chunk；eXIf仅受界IFD0、II/MM方向1–8 | APNG/动画；指针和非零nextIFD、完整Exif；全部合法PNG变体/真实画质未全验 |
| JPEG | 8bit baseline、单完整interleaved scan、1或3组件，H/V各1–2（灰度1×1、每MCU块数≤10）；IFD0方向1–8 | progressive/lossless/多scan/CMYK及其他编码；完整JPEG/Exif未完成 |
| BMP | 本轮BM+14/40头、24bpp BI_RGB、两种行序、精确offset54/stride/总长 | CORE/V4/V5、其他位深、palette、RLE/bitfields及gap等；完整BMP未完成 |
| WebP | 尚未接入此解码/预处理接口 | 静态和动画WebP均未完成 |
| GIF | 尚未接入此解码/预处理接口 | 原要求的首帧解码及“使用首帧”标注均未完成 |

原TIFF多页/HEIC/SVG/PSD/PDF文件本体不属于v1.0图像清单，未因本轮扩大。下一独立候选应限原清单中的受界静态WebP准入另行设计独审；GIF首帧标注和各格式余项继续显式保留，不能无限扩格式。

78项保护核对全真，含旧214测试、原SPEC/产品书、固定包/锁/许可证、原生代码/启动器/工作流、两个Coordinator及已有截图处理/映射/PNG请求组件。PNG/JPEG方向源码除显式BMP分支外完全相同。App/Imaging实际assets/output无Skia、Worker声明原字节相同。Windows截图/native/Worker/OCR/WPF、打开/投放、本地字号/真实质量及完整P6仍NotRun/未完成；Linux合成回路不替代这些证据。

250/600ms及资源、来源/密码/权限/完整性/取消/清理/隔离门、PackageHold/生产图片复用Off保持。无新依赖许可/权限/费用/凭据/CI调度/真实用户图片或模型/用户电脑/发布/main修改/强推；旧CI39347646426 step3被拒日志未请求或绕路。
