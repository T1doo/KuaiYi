# P6-02 最终 crop 有限归一化与 ImageTransform

基线 `f463dd419df2593eb0e799f64644c5b38d477458`，正常分支 `dev/screenshot-normalization`。先核真实文件/原实现、干净 Git、单 worktree/唯一写入者、HEAD 与默认网络远端祖先，main 保持 `51d077481cf6303602fcf5d2718213da9cf05178`。root 唯一写入、独审只读，不恢复旧实例，不动未知锁/进程。按 AGENTS、SPEC7.2/7.3、10.6/数据坐标合同与阶段门，仅推进既有最终截图 owner 的纯像素离线切片。

## 可发送边界与实际实现

`OwnedSourceImage.PrepareRequest` 只处理已经拥有的最终 crop，不读文件/URL，不取得全屏、网页、路径或 EXIF。原数据为明确的 straight BGRA；真实采集入口已有 alpha=255。每通道按 alpha 在固定白色上做整数合成，输出 opaque BGRA，再复用原生 lossless RGB PNG 编码器。透明像素的隐藏 RGB 不会进入请求，原 crop 不改。旧 `CreateRequestPng` 共用同一实现并保持超过2560一律拒绝的原合同；正常 Shell/截图卡仍仅本地预览，未接新发送。

`ImageTransform.Plan` 只规划等比 fit 与整数短边舍入，不能代表可读或可发送。长边≤2560，短边取最近整数且至少1；记录实际每轴比例与完整原 crop，不裁掉任何部分。实际准备的规则更严格：

- 原尺寸已满足边长时，保持1:1可视像素，仅白底合成。
- 需要缩小时，只允许**白底合成后全图每一像素 RGB 严格恒定**的极窄例外。全扫描、无采样/容差/平均亮度或边缘密度猜测，再生成相同恒色的完整fit图。这只能证明没有可视结构丢失，不能声称验证了文字可读性。
- 其余需缩小的图片返回 `SMALLER_SCREENSHOT_REQUIRED`，保原 owner/本地预览、零 HTTP。没有可靠 OCR、实际可读性评审，不能仅凭缩放比例自动放行文字/图表，不能先收费分类来论证本地缩图可读。
- 编码后≤4MiB及 profile 更严上限。PNG压缩/输出工作缓冲也使用这个上限，不能先无限编码；当前 lossless 编码不能满足则返回相同重截原因。没有降低质量循环、逐次缩小、悄悄裁边或预览图替代。

## 坐标、缓存与所有权

`ImageTransform` 保原 crop 物理边界（含负原点）、原尺寸、实际整数输出尺寸和 screenshot identity 方向。`OriginalImagePoint/Rectangle` 是 crop-local像素边缘，`NormalizedImagePoint/Rectangle` 是 prepared-raster像素边缘（供将来在同图上的OCR使用），`DesktopImagePoint/Rectangle` 是物理桌面边缘，`UnitImageRectangle` 独立表示分类 bbox 的 `[0,1]`。没有隐式互换。映射双向，使用 `[0,W]×[0,H]` 连续边界；拒绝非有限/越界/负宽高，不把像素中心、DIP、单位坐标混用。浮点内部边缘舍入不等于截掉输入。没有执行 OCR 或 EXIF 旋转；映射是本地元数据，不入 PNG chunk/请求提示。

source锁内仅做有界逐行私有复制；锁外合成/完整恒色扫描/缩小/编码，逐行检查取消。copy、归一化 BGRA、编码row、自持有压缩/输出缓冲和失败PNG在using/finally清零；`PreparedScreenshot` 拥有实际返回PNG，Dispose清同一buffer，source可先Clear独立释放。单份输入≤24MP/16384边，输出≤2560边与硬字节cap；没有本轮原生分配/取消毫秒或系统/压缩库内部副本物理擦除保证。

每个离线job只准备一次PNG/transform，分类与生成实际复用同一byte数组。postprepare仍过原完整身份/epoch/source owner/settings/binding/预算门；在原canonical State/Generating提交点写映射，再发 Prepared 元数据事件，通知后重核Eligible，Clear/Stop/Adopt/Lock/Hide或后继C不能触发旧阶段/恢复旧快照。分类合法完整后同时缓存其transform；再生成准备出不等价几何时失败，不把旧bbox套新图。分类usage继续引用原回执；三版、模式覆盖及其余取消/清理合同保留。

## 原始运行与失败

| 运行 | 实际结果 | 说明 |
|---|---|---|
| r01-white-red | 1通过/5失败，exit1 | 原编码忽略alpha，透明/半透明实际RGB不符合白底；opaque已通过 |
| r02-normalization-fixed | CS0121编译失败，0执行，无TRX | point/rectangle的target-typed new重载歧义；原log与当时源码保存 |
| r03-normalization-expanded | 185/185 | 初版纯像素/映射与全部旧owner/编排 |
| r04-normalization-integration | 211/211 | 双协议同图与Prepared取消/迟到/C重入 |
| r05-coordinate-space-red | 0通过/1失败，exit1 | 独审发现公共mapping复用同类坐标，违反独立空间合同；原源码/测试保存 |
| r06-spaces-fixed | 216/216 | 独立强类型修复，补已缓存分类的Prepared Stop/C分支 |
| r07-provider-final | 1306/1306，0失败/跳过 | 保父1232测试名多重集合，新增74：44归一化、30实际编排 |
| r08-app | 0警告/0错误 | 最终Release/no-restore/EnableWindowsTargeting交叉构建 |

直接Link生产归一化/owner/encoder/App组件，真实Core/Provider和既有Kestrel双协议HTTP；PNG实际解压/CRC/仅IHDR-IDAT-IEND检查，原图/归一化/返回PNG持有后清零、随机1000坐标往返与非法边界、恒色/隐藏RGB/任意位置单单位差异、严格cap/取消、双阶段同PNG与实际bbox映射、缓存/模式、排队清除及无缓存/已有缓存的C重入均执行。输入均为合成公开数据，不是实际截图/图文质量样本或真实模型。

保存14份SDK log/TRX和6份两轮红测/编译前源码，唯一命名并精确 `-text`；父142份原始回执及1724份既有证据不改。初红owner/encoder与父提交完全一致，新的6项白底测试后来完整保留；坐标红测断言修后保留，旧有测试名无删除。所有计数、hash、原件和独审绑定见[固定证据](../test-evidence/2026-10-16-screenshot-normalization.json)。

## 明确保留的缺口

P6-02维持InProgress及原validation **Blocked**；P6-04/05维持InProgress/NotRun。完整PNG/JPEG/WebP/BMP/GIF样本、EXIF/元数据处理、隔离解码与SkiaSharp准确版本/许可证审查需后续独立锁定，本批没有新增解码器/依赖或将P0头检查冒充完整解码。一般文字/图表下采样可读性、P6-03 OCR及图像Beta未完成。

Linux无WindowsDesktop/交互桌面，真实WPF测量/排版/选择/阅读/焦点、Win32/COM所有权与时限、采集/多屏/OCR/真实模型及视觉质量、完整P2/P4/P6/I/J仍NotRun；完整Core.Tests项目的未知CMake根因未重试。交叉构建与像素fixture不能替代这些门。

250/600ms及来源/密码/完整性/取消/清理门、生产离线复用Off保持；Core、Provider传输/协议/usage、8模板、8workflow、43锁不改。dotnet激活前后export且每次命令前显式`DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，旧证书存储/信任和覆盖前字节Unknown，不读库/配置/trust/删除/重造。Actions不查询调度、不绕Forbidden，远端run数Unknown。无main/强推/部署/新费用凭据权限协议、真实模型或Windows/CMake/新CI实验。
