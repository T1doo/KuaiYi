# P6-02：隔离 PNG / 有限基线 JPEG 解码组件

2026-10-20，基于 `a1da3de6ecfa2626540b1c63dc50a2880348b978`，正常分支 `dev/p6-isolated-raster-decode`。本批实际交付一次性 ImageWorker 内的解码组件，以及父进程限量接收的 owned BGRA 栅格；原 SPEC P6-02 整项仍未完成，P6-06 尚未接 UI。新环境 Linux x64，唯一 writer、单 worktree。原 SPEC、旧验证原件、安全门和发布状态保持。

## 源码与依赖前置已取得新证据

本批在上一份[源码门记录](p6-codec-source-review.md)之后收到受信用户证据：重新发布指定的非 x-plugin 环境并开启网络，当前为更新的指定环境。独立复核该实质变化及此前新环境精确目标请求0后，按授权对原 GitHub tree/release URL **各一次**有界请求，均 HTTP200/exit0；无重定向、重试、镜像、代理/凭据/安全权限变更。旧403仍作为历史事实保留，此次200只证明本次这两个目标可读；旧 Windows CI39347646426 step3 的被拒日志未请求或换路。

固定 SkiaSharp commit `4783f51448f9b070dda4f87b83e941c9599e466e` 的官方递归树6317项、truncated=false；选定23份源码正文均重算 Git blob SHA1并匹配官方 API。取得原生构建/打包配置、API实现及固定 `externals/skia` gitlink `45afab4f1f0921f3feb97f58cf89b136fffa85e6` 的 DEPS；cgmanifest 中 libpng/libjpeg/webp/wuffs/zlib revision与DEPS匹配。所读 shared native 构建关闭 dng_sdk/piex，Linux.NoDependencies关闭fontconfig。这些是固定源码配置证据，**不证明已发布二进制可重复构建或所有组件安全**；完整第三方声明没有因开关而删减。

用户已批准的四个4.153.1官方包重新取得，字节/摘要及ZIP内容与10月16日原清单一致；SDK `nuget verify --all` 四包均成功，无NU警告/错误。独立审查准入限定指定云开发、固定包、隔离解码及合成测试。中央版本和 Worker/新夹具/新测试锁已固定；仅 Worker 的既有锁变化，其他既有锁不动。完整原 LICENSE/THIRD-PARTY-NOTICES 逐字节保留在 `licenses/SkiaSharp-4.153.1`，实际 Worker 输出副本相同；未执行 publish。

## 实际行为和边界

新增私有消息107–109，复用原 hello、同一 inherited stdio、一次性 job、所有权和实际退出门，不进入外部命令网关。输入 session/version/epoch/长度/SHA256绑定；限制保持20MiB、单边16384、24MP、64KiB JSON及1MiB binary。输出为紧密 top-down straight BGRA8888，显式像素语义、frames1、orientation1、尺寸/stride/总长/摘要，有限顺序分块及唯一commit。父端先验元数据再分配，要求格式/尺寸与输入头相同。

PNG检查首个/唯一IHDR、chunk边界/CRC、存在IDAT及精确最终IEND，拒绝acTL；其余解码语义仍要求原生 `GetPixels` 完整Success，不声称独立实现全部PNG语法。JPEG只接受有限8bit baseline、单完整交错扫描、灰度或三分量的受限采样；结构Huffman扫描检查实际MCU/系数码、DQT/DHT、stuffing、restart、padding及精确EOI，再由Skia真正解像素。渐进/算术/多扫描等合法变体明确Unsupported。非TopLeft EXIF和动画亦明确不支持，没有伪造方向变换或首帧支持。静态codec FrameCount0依据固定C API的getFrameInfo().size()解释，不能误当动画拒绝。

所有解码仅在Worker；App/Imaging没有Skia包引用，实际assets libraries/targets及输出均无Skia。父端取得完整栅格后仍持有，只有stdout精确EOF、实际childExit0、原IO结算及取消门通过才交付。extra-byte、第二commit、非零退出、完成后挂起再取消均不能交付，实际持有数组清零。Worker持原decode/control task直至结算；取消晚返栅格同样清零。保留原15s与进程终结门，进程隔离不等于已证明安全沙箱或原生内部内存清零。

## 本批实际验证

| 实际运行 | 结果与范围 |
| --- | --- |
| 新codec最终构建测试 | 49P/0F/0skip，Linux真实子进程链接生产Program/Session/decoder源码 |
| 最终原产物 `--no-build --no-restore` 重跑 | 同49P/0F，不与上一行累加 |
| 原Core相关filter | 74P/0F；Worker/startup/finalization/IPC，非全Core |
| 原Provider传输filter | 37P/0F，非全Provider |
| Worker与App Release交叉构建 | 各0警告0错误；App八个相关项目；不代表Windows加载 |
| 五个相关 locked restore | Worker/codec/Core/Provider/App，审计all/low且warnings-as-errors，全部exit0 |

有效样本含真实PNG/JPEG、透明straight alpha、>1MiB输出、灰度/4:4:4/4:2:0、非整块尺寸及restart。恶意/异常样本涵盖CRC/容器/压缩流截断、坏熵码/表/量化/padding/restart、额外数据、超尺寸、身份/摘要/布局/块序/提交、退出后缀和取消清理。受控取消端口测试明确用于原task晚返/错误持有，不冒充原生时限或Windows。

保留所有中间失败：首次新实现27项20P7F（静态FrameCount误解），修正后27P；旧实际Worker运行新有效PNG/JPEG方法2F，最终同方法通过；扩展37项36P1F发现Skia会将截断JPEG扫描修补并报Success，增加有界熵流检查后同方法通过。两个因果方法正文与原失败快照逐字节相同，没有弱化断言。首次扫描器编译CS8175/CS8793/CS0019属于0执行，原源码和日志亦保留。后续48P及最终49P分别记录，不叠加、不抹失败。最终独立只读源码/回执审查无剩余可确认P1/P2；测试由writer执行，reviewer未重跑。

## 尚未完成

全JPEG模式、WebP/BMP/GIF首帧、EXIF方向坐标变换、一般缩放/白底合成、P6-06打开/投放入口、Windows实际native加载/Worker/OCR/WPF与完整P6验收仍未完成。PowerShell入口已静态接入新codec项目，本机无pwsh，未运行该脚本。生产图片复用仍Off，PackageHold保持。250/600ms及来源、密码、完整性、取消、清理、隔离门未改；无真实模型、CI调度、费用、凭据/安全权限、部署、发布或用户电脑操作。

原始官方源码响应（排除HTTP原始头）、签名/构建/测试原件与失败快照见[证据索引](../test-evidence/2026-10-20-isolated-raster-decode.json)。许可证和相关源码须随普通构建交付保留，发布仍需后续单独满足全部条件。
