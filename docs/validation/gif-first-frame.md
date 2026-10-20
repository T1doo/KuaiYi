# P6-02：受界 GIF 首帧与明确的本地预览标注

2026-10-20，指定保存环境的实际初始工作区为 `work` / `51d077481cf6303602fcf5d2718213da9cf05178`，并非上次缓存。只读远端核到 `dev/p6-static-webp` 为 `0fafe74760958b78302bbc58ae51dcdfd7d9d55e`；fetch后验证初始HEAD为其祖先，再从精确SHA接续。单worktree、唯一root writer；旧实例未恢复，未知 `.git/codex-index-refresh.lock` 未删除。最终工作分支 `dev/p6-gif-first-frame`。完整SPEC及相关验证已阅读，原SPEC保持原字节。

## 已实现的组件合同

`DecodeAsync / PrepareAsync / PreparePngAsync` 接受本切片的 GIF87a/89a，先扫描**完整原容器**：逻辑画布、全局/局部palette、每帧矩形、GCE、子块终止及精确trailer/EOF。原20MiB、24MP、单边16384界不变，另有限256个image descriptor、4096个顶层block（包含trailer）、131072个子块（包含各terminator）。矩形非空并完全在画布内；声明长度先校界，offset使用有界/checked算术，不按后缀接受文件。

保守子集拒绝非零pixel-aspect、无palette、越界透明index、GCE保留位/重复/悬空、用户输入标记、disposal4–7、plain-text/未知extension或application。GIF87a的extension也拒绝。允许受界comment，以及唯一、精确形状的NETSCAPE2.0/ANIMEXTS1.0 loop extension；不执行其内容，不传播任意comment/path/GPS字符串。格式字段参考[GIF89a规范](https://www.w3.org/Graphics/GIF/spec-gif89a.txt)，这里的数量上限、严格EOI padding及部分合法特性拒绝均是产品子集，不宣称全GIF兼容。

仅Worker对**第一帧**运行固定4096词条LZW完整性检查：initial Clear、码宽3–12、引用、KwKwK、Clear重置、字典饱和、palette符号范围、展开长度恰等于首帧矩形像素数、EOI及子块边界。扫描器累计词条长度和符号范围，不展开第二张栅格。EOI之后仅允许不足一byte的**零padding**、紧接子块terminator；合法非零padding或额外payload也可能被此严格子集拒绝。

后续帧只检查容器、矩形、palette/GCE、minimum code size和子块结构，**不检查其LZW完整性，不解码**；因此后续帧熵流无效但结构完整的文件可接受。`SourceFrames`计数是结构中的image descriptor数量，不是所有帧可成功解码的证明。

native输入是已验证原字节到首帧data terminator为止的精确前缀，再加trailer；后续帧从未交给native。完整原job长度/SHA仍绑定未删改原文件。固定SkiaSharp4.153.1实际GIF格式、原逻辑画布尺寸、TopLeft、首帧副本FrameCount1均须匹配；唯一一次GetPixels显式 `FrameIndex=0 / PriorFrame=-1 / ZeroInitialized=Yes`，仅接受Success。API形状及native Options语义复核沿用已存固定版本源码原件，非凭在线最新版推断。

第一帧输出完整逻辑画布，保留偏移、局部palette和透明像素；矩形外显式清为透明，LSD background index不被当作填充色。prepared仍用原白底/完整area内核，默认长边2560、可更严，encoded transform不伪造桌面锚点；请求PNG仍只有IHDR/IDAT/IEND、默认4MiB或更严，不新增质量/尺寸重试。

## 可信元数据到实际可见位置

输出 `Frames`仍固定1；GIF必须携带纯数字 `GifFirstFrameInfo(SourceFrames,Left,Top,Width,Height)`。父端从完整原输入独立计算并在分配栅格前精确比较；失败或非GIF携带该信息直接拒绝。元数据通过prepared raster及PNG owner保存，固定 `Notice` 为“使用首帧”，单帧GIF也显示。

现有 **P0本地实验窗口**实际接入一个独立按钮“GIF · 使用首帧 · 本地预览”。在已有Windows实验入口条件满足时，以 `KuaiYi.App.exe --platform-review` 打开，先明确允许本次本地操作，再选单个本地GIF。读取复用原文件handle/最终目标/本地drive/重解析/大小门；容器扫描和Worker准备在后台，最长边512的实际BGRA填充WPF bitmap。窗口标题、可见TextBlock和可访问名称均明确“使用首帧”；仅本地预览，未识别或发送模型。

投递仍经完整session/version/request/epoch及RuntimeGate，只有原IO、完整EOF和实际exit0后可显示。取消回调绑定该次preview owner；清理失败保留原bitmap owner，并阻止GIF/PNG/截图/OLE/browser新采集和窗口关闭，原位可重试清理，不能宣称已清空。独审发现的两个P2（晚取消回调未绑定原owner、残留bitmap未阻止其他采集且状态误称清空）均已源码闭合。WPF显示、交互取消和bitmap清理**尚未在Windows运行**；这里只交付实际可达代码与交叉构建，不制造截图或桌面通过记录。

生产ShellWindow打开/投放、GIF到生产分类/生成/OCR链及统一图片来源展示仍属于未完成P6-06；本轮没有借截图owner为文件伪造坐标，也没有把P0窗口当作已完成图像Beta。

## 实际验证与证据

原始日志/TRX、最终源文件和产物摘要见[证据索引](../test-evidence/2026-10-20-gif-first-frame.json)及其ZIP。SDK保存缓存仅10.0.401；首次按原global.json启动拒绝执行restore，0测试。随后通过Microsoft官方固定10.0.402 Linux包及SHA512核对恢复现有隔离SDK路径，runtime10.0.13；未修改global.json或降低安全runtime要求。相关五次locked restore均exit0，NuGet audit all/low、warnings-as-errors，所有锁文件原字节不改。

| writer真实执行 | 结果 |
| --- | --- |
| r02 同一GIF完整画布方法，旧产品基线 | 0P/1F，UnsupportedImage |
| r03 同方法，实现后 | 1P/0F |
| r04 / r06 / r08 GIF组 | 83P / 89P / 91P，各0F、0skip；不累计 |
| r10 完整Codec最终回归 | **453P/0F/0skip = 新GIF91 + 原362** |
| r13 Provider传输限定filter | **37P/0F/0skip**，非全Provider |
| r15 Core相关限定filter | **165P/0F/0skip**，非全Core |
| r11 Worker / r16 App Release交叉构建 | 各 **0W/0E**，非Windows实际运行 |

包含真实链接生产Program/Session/decoder的Linux子进程。完整非恒定画布、87/89和global/local组合、非零背景index/offset/透明与disposal0–3、interlace行、白底、2×area、metadata-free PNG、最大256帧、实际16384×1逐像素、精确24MP头部接受（非24MP实际解码）、43种结构拒绝、首帧8种熵流拒绝、原job/hash、nativecopy清零、错源metadata分配前拒绝、extra-byte/exit7阻止编码、完整栅格后hold-exit取消及实际持有数组清零均验证。

新增独立Pillow256色/interlaced固定合成GIF及编码前原indices核完整32768像素；专门的代码序列另核全部码宽、4096饱和、词条4095、Clear后重置及EOI跨子块。夹具来源/算法/摘要见[fixture说明](../../tests/fixtures/images/README.md)，不引入测试运行依赖。91中间绿不与453最终绿累加；首红未删除或改成未运行。独立reviewer只读设计、源码及writer回执，未声称另跑测试。

85项基线保护全真：原六个Codec测试文件、原SPEC、锁/版本/许可、原Worker传输/进程退出门、生产截图发送/来源owner、WebP policy等字节不变。App/Imaging实际NuGet assets和输出无Skia；Worker输出完整原LICENSE/THIRD-PARTY-NOTICES字节匹配，未执行publish。

## 仍未验与原约束

Windows真实native/Worker/WPF/OCR、真实画质/本地字号检测、250/600ms与600MiB、生产打开/投放和完整P6均未验收。GIF只支持上述严格子集，原PNG/JPEG/BMP/WebP支持边界保持；WebP合法odd payload仍明确拒绝，未放宽固定native padding门。PackageHold、生产图片复用Off以及来源/密码/权限/完整性/取消/清理/发送门保持。

无新第三方依赖/许可、费用、凭据或安全网络权限、真实模型/私人图像、CI调度、用户电脑、发布或部署操作；不改main、不强推。旧CI被拒日志未请求或绕路，未知锁文件/进程未删除。
