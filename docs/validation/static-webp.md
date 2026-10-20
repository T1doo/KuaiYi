# P6-02：原格式清单中的受界静态 WebP

2026-10-20，从已独审、clean、远端一致的 `767af3ee45a077f0a5c9216aec60f10fcc3ca73b` 接续，单worktree、唯一root writer，正常分支 `dev/p6-static-webp`。原SPEC7.1包含WebP，本轮接入隔离解码/预处理组件，未扩大原格式清单。

## 接受与拒绝合同

本切片仅接受**图像chunk载荷长度为偶数的静态 VP8 / VP8L**，可带唯一首位、长度10的最小VP8X。RIFF签名/WEBP类型、size+8等于实际总长、总长偶数；每chunk的长度和含padding末端用checked64位计算，先核界再转int，odd padding必须0，精确EOF，至多256chunk。原20MiB、24MP、单边16384上限保持。VP8X保留位和保留字节必须0，只允许alpha标志；24位canvas尺寸必须匹配位流原尺寸。

VP8必须完整10字节keyframe头、版本0–3、show、sync、非零14位宽高、无scaling；first-partition大于0且不超过payload减10字节。VP8L必须0x2f、完整5字节头、version0，14位尺寸加1；alpha hint不决定实际像素透明度。这些字段来自[WebP容器定义](https://developers.google.com/speed/webp/docs/riff_container)、[VP8L位流定义](https://developers.google.com/speed/webp/docs/webp_lossless_bitstream_specification)及[VP8字段定义](https://datatracker.ietf.org/doc/html/rfc6386#section-9.1)。精确EOF、元数据/未知chunk拒绝、even-only均是更严格的产品子集，不能称所有合法WebP变体支持。

所有animation flag、ANIM/ANMF（含仅一帧）、EXIF/ICCP/XMP及对应flags、ALPH、未知chunk明确Unsupported，不默默忽略方向或颜色信息。VP8X alpha=1配VP8而无受支持ALPH也拒绝；重复/混合image、非首位或重复VP8X、canvas冲突、保留位、长度/末尾/零padding错误拒绝。原始source/job长度和SHA仍绑定未修改字节；native copy逐字节相同，没有alpha改写或双解码。

Worker必须实际报告Webp、原尺寸、TopLeft、静态帧数0或1和GetPixels Success；source方向为identity。raw保持straight BGRA及实际alpha，prepared沿用原area/白底内核与无桌面锚点映射，PreparePng沿用单次metadata-free RGB PNG及4MiB或更严API界。父端与Worker均在native copy前验证；原来源/权限/身份绑定、分块/摘要/commit、EOF/实际exit0/原IO结算、取消晚返及自有buffer清零保持。

## 实际发现与保守收紧

首轮同步修正RIFF/chunk长度后，46字节VP8L删除最后1字节，native仍Success，但完整像素SHA改变。独立native探针再次证明，该45字节payload的zero/FF padding导致不同像素。合法19字节payload两种padding的像素则相同；去掉padding导致native创建失败，不能简单删pad。

固定Skia子模块 `45afab4f1f0921f3feb97f58cf89b136fffa85e6` 的[SkWebpCodec](https://github.com/mono/skia/blob/45afab4f1f0921f3feb97f58cf89b136fffa85e6/src/codec/SkWebpCodec.cpp)将fragment交给WebPIUpdate；固定DEPS中libwebp `4fa21912338357f89e4fd51cf2368325b59e9bd9` 的[demux](https://chromium.googlesource.com/webm/libwebp/+/4fa21912338357f89e4fd51cf2368325b59e9bd9/src/demux/demux.c)把padded size计入fragment。两份普通只读官方源码各一次HTTP200及摘要已存；不涉及旧被拒CI日志。

独审后采用更小保守边界：**所有odd image payload显式Unsupported，包括合法odd文件**。接受的even payload精确结束于原文件末尾，没有容器padding可借用；不引入第二栅格、双解码或新库。0/FF两点输出一致不是一般完整性证明，未将其作为生产门。仍要求native完整成功，但不声称独立验证了任意熵流合法性。VP8删除末尾1/3字节的原样本像素完全相同，不能把移除尾零冒称真实损图；其odd子集拒绝也单独记录。

固定native对VP8L alpha hint及最小VP8X alpha flag所有组合均保留实际alpha，因此无需改写这些位；真实child raw/prepared也分别核验。Windows原生库行为仍未实测。

## 本轮实际验证

最终命令、TRX、失败因果、当时源文件及产物摘要见[证据索引](../test-evidence/2026-10-20-static-webp.json)。

| 本轮最终运行 | 实际结果 |
| --- | --- |
| Codec Release构建测试 | 362P/0F/0skip = 新WebP76 + 旧286；旧五个测试文件字节不改 |
| Provider传输限定filter | 37P/0F/0skip，非全Provider |
| Worker/App Release交叉构建 | 各0警告0错误，非Windows实际运行 |
| Worker/Codec/Provider/App locked restore | 四个exit0，audit all/low及warnings-as-errors；不改依赖/锁 |

长边最终为确定性语料中的16383×1非恒定彩色图，完整65532字节逐像素相等；不把编码器无法生成的16384样本说成解码通过。80项基线/祖先保护核对全真，App/Imaging实际assets和输出无Skia，Worker输出的原许可证/第三方声明字节相同。测试为writer实跑；独立审查为只读源码及回执审查，不冒称独审再次执行。

覆盖完整3×2非恒定画布到最终PNG；六种simple/minimal-VP8X hint/flag组合的实际alpha和白底；非灰度半透明straight BGRA与独立逐通道白底算术；VP8不透明整画布；5×3→3×2非整数area。43种结构/不支持拒绝发生在child/native copy前，另核合法odd两种wrapper及原真实截断。错原job/hash、错source、额外输出、exit7、完整栅格后取消均不能交付或提前编码；实际copy在成功/取消/observer异常/受控native拒绝时清零。

首测试编译有未读output与nullable错误，0执行；修正后原基线功能1F、native透明探针1P。首WebP组51P9F：6条误复用single-use client已改两个真实client，3条截断预期触发上述调查，原件保留。even收紧后75P1F，唯一F是生成16384宽测试样本时固定encoder返回null，尚未执行产品解码；随后16383规则图案只生成odd payload，两条夹具尝试和一次提前排入的完整361P1F也保全。最后使用有界确定性合成语料选择even长图，断言仍逐像素等于所选原输入；不把夹具失败称为生产缺陷，不证明16384边界实际解码。合成样本选择不进入产品请求编码，产品无降质量或重试。

## 原五格式当前边界与剩余项

此表只指 `DecodeAsync / PrepareAsync / PreparePngAsync` 组件；入口允许文件不等于打开/投放UI、真实画质或Windows产品验收。

| 原格式 | 当前组件范围 | 仍未支持/未验 |
| --- | --- | --- |
| PNG | 静态受界容器/CRC/native像素；IFD0 eXIf II/MM方向1–8 | APNG、嵌套/完整Exif、全部合法变体/真实画质 |
| JPEG | 原8bit baseline单完整interleaved scan及受界IFD0方向 | progressive/lossless/多scan/CMYK、完整JPEG/Exif |
| BMP | BM14+INFO40、24bpp BI_RGB、offset54、两种存储行序、精确stride/总长 | 其他头/位深、palette、RLE/bitfields、gap |
| WebP | 本轮even-payload静态VP8/VP8L，最小VP8X；实际透明/白底/area | **合法odd payload也拒绝**；ALPH、EXIF/ICC/XMP、动画、其他合法变体；16384解码未实证 |
| GIF | 未接入 | 首帧及“使用首帧”显式标注均未完成 |

下一独立候选为原GIF首帧与明确标注的最小接入，须另行设计独审；不扩TIFF/HEIC/SVG/PSD/PDF或无限格式变体。打开/投放UI、本地字号/冻结真实质量、Windows截图/native/Worker/OCR/WPF及整P6仍未完成/NotRun。250/600ms未实测通过；原资源与来源/密码/权限/完整性/取消/清理/隔离门、PackageHold/生产图片复用Off保持。

无新依赖、锁/许可修改、费用/凭据/安全权限、CI调度、真实模型/私人图像/用户电脑/发布/main修改/强推；旧CI39347646426 step3被拒日志未请求或绕路。
