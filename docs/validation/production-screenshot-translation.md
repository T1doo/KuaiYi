# 正常 Shell 最终截图 → 显式图译

从已推送父 `78e7db0d5caa5ea77625877dbddd2252562cb047` 正常建立 `dev/production-screenshot-translation`，先核实际 HEAD、单 worktree、默认远端及祖先。父链包含最初 d45ba9d；main 保持 `51d077481cf6303602fcf5d2718213da9cf05178`。这是已授权 P6 的最小生产接线，不代表完整 P6 验收。

已有最终裁剪图进入正常 Shell 后，用户可从工具栏或菜单选择本次智能判断、仅翻译文字、按图文解释。截图接收、预览、打开菜单及设置都不自动发送。每次实际发送前显示视觉端点、模型、当前条模式及通常分类＋生成两次调用/可能费用说明；仅明确 VisionProfileId 且 Supported 的配置可用，不借用文字配置或开发探针权限。普通 ApiProfileMapper/ProviderClient 与 SettingsCoordinator.OpenRequestSecretAsync 接线；两个阶段各持有短期秘密租约，同一绝对预算，发送及交付保留来源/配置/秘密绑定/授权和完整 identity 检查。

仅借用 Shell 已有 OwnedSourceImage，按现有受限截图预处理生成同一 PNG 供两阶段使用。没有通用文件/Drop 解码、新 codec、Skia 安装或生产图片/分类复用。当前源一旦启动便不可再次发送；清除不重新开放这个源。分类必须完整且合法；按既有明确授权，非法/未完成/超限分类关闭流程，不自动增加正式生成调用；合法 uncertain 显示不确定解释说明。当前条模式在首次发送前选择并冻结，不修改全局风格；旧图重新生成仍关闭。

Shell 正文与当前原图预览共存，显示该版本模型、风格、不确定说明及分类/生成各自服务端 usage，Unknown 不估算、不汇总费用。取消、停用、隐藏、锁屏、来源替换和配置变更拒绝晚到，显示保存仅沿用既有受限 Overlay/WriteId 兼容规则。失败自动显示最新尝试原因，手选失败读自身；截图 A 完成后 OCR 文字 B 失败自动回 A，仅当 selected image snapshot 的 SourceVersion＋SourceId 精确匹配仍持有原图时保留 A；普通文字回退释放分支保持。

## 实际验证

所有图像、原文、译文、模型名及秘密均为公开合成夹具，HTTP 仅本地 Kestrel。没有真实模型、费用或用户凭据配置。

| 固定原件 | 实际结果及含义 |
|---|---|
| r04 → r05 | 失效 destination 排队后 Clear，原任务未结算 **1F→1P**；finally 不再等失效所有者，保留原 cancellation。 |
| r06 → r07 | 取消回调试图在 Clear 中安装 C **1F→1P**；清理 fence 拒绝重入，异常仍 join 原任务。 |
| r11 → r12 | Generating 回调耗尽共同预算，原实现仍开第二秘密租约，两协议 **2F→2P**；租约前后检查，同预算不重置。 |
| r15 → r16 | 实际截图两次 HTTP 完成 A；实际合成 OCR 批准后文字 B 经 401 或真实 SSE 部分正文＋length 失败，精确正文/失败原因/自动 A 断言通过后在 A 原图释放处 **2F→2P**。修后切 A/B 各自正文与原图/OCR 原文匹配。 |
| r17 | 最终专项 **60P、0失败/跳过**。 |
| r18 | 无筛选完整 Provider **2272P、0失败/跳过**＝父2212全名多重集＋最终60。 |
| r19 | 无筛选完整 Core **1703P、0失败/跳过**，与父全部结果名多重集相同。 |
| r20 / r21 | 实际 App / ImageWorker 交叉构建各 **0W/0E**。 |

每组红绿的指定测试文件 SHA 与结果名多重集相同；不是声称测试 DLL 整体相同。最终 r17—r21 的21项源绑定都等于当前源。run.py 的 DLL 字段也会观察其他已有输出，只有各命令目标闭包实际执行/构建，不能把其中陈旧无关 DLL 当作本次执行证据。

失败和中间候选完整保留：r01 为 CS0200；r03 为 CS8604/CS0649，无执行/TRX；r10 是实际 App 0W1E，修 namespace 后最终重建。r02 未完成，记录精确 owned PID199394/祖先后仅向该测试发送 SIGINT，exit1、无权威 TRX；未捕获当时等待栈，不宣称确切挂起根因。r08 为50P1F，新停止测试误期待晚到接收成功，只把该断言 True 改为要求的 False，保留原正文/原图/停止断言，不算产品红绿。r09 53P、r13 58P都是中间候选。r14 两例先复现原图释放，但未精确断言部分正文，不能与加强后 r16 混称同源红绿。

两协议实际 JSON/SSE/认证头、同 PNG、秘密租约清零、每阶段 usage、模式/不确定/非法分类、确认失效、秘密等待/取消、来源与设置撤销、通知重入 C、队列晚到、原图清理都有专项。秘密用例通过实际临时 SettingsCoordinator/SecretFileStore＋显式 portable AES fixture，不代表 Windows DPAPI/ACL。Shell 用例链接实际发送命令、结果桥、来源选择、Markdown 与标签，WPF/native 与确认 UI 为明确端口；源码真实 MessageBox/菜单经交叉构建，未执行可见 UI。OCR 用例实际 OcrOnlyCoordinator 批准及 TextTranslationCoordinator；本地识别器合成，linked raster/aliased Imaging 各自公开像素副本、独立 SourceId，不冒称 Windows OCR。停止正文用例显式喂 presentation port；回退用例另经真实 SSE 验证部分正文。

独立只读 high_water_review 最终源码/测试审查无剩余 P1/P2；封存终审回执另列。可选补测“租约 await 内推进 fake clock 后返回”未执行，不据此声称有该独立用例；await 前后检查及双协议阶段预算红绿已核。原件、SHA、命令、源码、TRX/DLL 观察和所有未完成尝试见[证据索引](../test-evidence/2026-10-17-production-screenshot-translation.json)。全部旧测试断言、SPEC/原产品书、traceability、旧证据、OfflineScreenshotCoordinator、Core/Providers/Imaging 原字节保持，状态不抬。

真实 Windows 截图/多屏、WPF 键盘/焦点/可见交互、COM/native、DPAPI/包身份、真实模型质量与物理250/600ms及完整 SPEC 验收均未执行。一般解码、完整预处理、图片复用/重新判断/重生成及完整 P6 仍未完成。旧图片间歇根因、历史 Core 当次结算条件、Windows Job 未清空根因独立 Unknown。仅已有 SDK/--no-restore，激活前后禁证书生成；无新包/许可/环境、main/强推/部署/费用/真实凭据/安全权限/真实模型，Skia四包许可待批、源码403及 Actions 先前拒绝不查不绕，未知锁/数据/日志/进程保全。
