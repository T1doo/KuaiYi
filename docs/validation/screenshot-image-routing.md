# P6-04/05 独立离线截图分类与调度子项

基线 `97a17e7f4f3f201a15471936881681a26f73f4d9`，新分支 `dev/screenshot-image-routing`。先核实际文件、干净Git、单worktree/唯一写入者、HEAD和默认网络远端祖先，未假定跨容器共享；main保持`51d077481cf6303602fcf5d2718213da9cf05178`。独审只读，未知锁和进程不动。

## 阶段依赖与当前原生环境

已读SPEC7.1—7.6、9.7/9.8、16.1及P2/P4/P6任务/退出门、16.11/18.1和AGENTS。SPEC16.1保留P0→…→P6依赖，16.11/18.1与AGENTS允许继续不依赖Windows/凭据的纯状态与协议切片，不能将子项测试改写为完整阶段验收。

本次实际指定环境为Linux x86_64、无DISPLAY/WAYLAND，SDK10.0.401，仅NETCore/AspNetCore10.0.12，无WindowsDesktop共享框架或Windows交互桌面。因此P2的B组/G01—04、B06/B07/B12真实布局/选择/阅读/焦点、P4真实取词→协议→浮窗的Alpha及原生质量验收仍NotRun。真实模型、身份OCR/语言与多屏截图/COM所有权也没有运行。不安装兼容层、不扩权、不绕隔离，不造验收工具/Windows CI，不重试未知CMake根因。

本批只交付 **离线功能组件**。`OfflineScreenshotCoordinator`没有接入正常Shell、截图快捷键或菜单；普通截图卡仍本地未翻译、未发送。P6-04/05标InProgress、整体验证NotRun，P6与生产验收没有放行。缺少的生产输入是P2/P4原生质量门、P6-01/02/03完整截图/规范化/OCR及图像质量证据、正式用户界面和后续阶段集成，不拿离线fixture冒充这些合同已完成。

## 功能与合同

- 严格分类：只接受完整JSON对象和五个精确字段；kind/readability精确枚举、bool、所有字段必需且拒重复/未知；最多12个唯一region、有限id/position/note、四个finite bbox数值且在归一化边界内，无JSON围栏/附加说明/嵌套任意结构。最多8192字符、深度6；矛盾的text_only/meaningful-structure组合拒绝。分类JSON只在内部累积，不进结果正文。
- 本次用户明确要求“分类不合法或不完整fail-closed”，优先于SPEC7.4历史的非法结构自动降解释：非法/缺字段/超限/协议未终止时直接失败，**不发第二次请求、不修复JSON循环**。合法`uncertain`才标记“图像结构判断不确定”并解释；unreadable要求更清晰/更小截图，不生成文字猜测。
- 原图合同：仅接已有最终`OwnedSourceImage`截图owner，不接文件/URL/浏览器、EXIF解码或新入口。复用`ScreenshotPngEncoder`无损编码；只允许完整crop长边≤2560且编码≤4MiB/profile更严限制，超出明确失败，不用1024预览当请求、不缩放/裁掉部分内容。编码用独立有界像素副本，原图可在Clear释放，PNG/传输字节在job finally归零。不是完整P6-02多格式预处理。
- 传输合同：构造必须提供既有不可变`DevelopmentTranslationBoundary`，组件自持同边界的ProviderClient，固定loopback/IP、AuthNone、TEST_ONLY_FIXTURE、无凭据/proxy/DNS扩展。显式On、显式用户动作、视觉能力必须Supported；普通/Unknown/Unsupported/远端/带secret引用配置均不能开始。截图接纳与模式编辑不发送；没有正常生产实例或自动外发。
- 正常一次分类、一次生成复用既有双协议image/base64序列化，不新增适配器或第三方依赖。两个阶段都过原canonical Gate和发送前source/version/owner/settings/binding/epoch/取消/Hidden/Suspension守卫，共用一个单调total budget，保留原有限HTTP重试/协议截止。纯文字没有可靠OCR时仍带同一图按所选风格翻译；混合/视觉/uncertain最终都带同一图解释，不能以OCR串冒充图文理解。
- 路由/提示：复用base/image-classify/image-explain/四风格/regenerate资源；区域和模型note只作为不可信user资料，不能成为system或执行指令。模式覆盖只在当前图片会话、只影响下一次显式生成，不改全局风格；各版快照固定。再生成复用所选分类/原图及会话覆盖，只有显式重新判断才再分类；新图重置覆盖/缓存。实际OverlayResults保留最多三版。
- 所有权/生命周期：Adopt明确转移最终crop，重复转移拒绝；Stop保当前文字与原图，只有新的显式动作才再生成。Clear/换图/Lock/Hide同步撤销请求并清原图、版本、缓存；ClearAsync/Dispose再等待调用时已持有的旧jobs，PNG/传输副本在各job finally完成释放；取消回调错误也继续清owner、保留/聚合异常，不能假报成功。当前源失效/排队旧AI/旧阶段不能启动final或恢复已清内容。字符串/系统副本不作绝对物理擦除承诺，原生取消时限未验证。

## 实际回执与红测

最终Provider **1118/1118**、0失败/跳过；父1014测试名多重集合完整保留，新增104：55分类/路由、45实际编排、4原crop导出/所有权。App Release两次交叉构建均 **0警告/0错误**。新编排测试直接Link实际App组件，复用真实Core/Provider/Gate/PNG；使用既有PhaseHttpFixture真实Kestrel loopback socket，双协议流式/非流式四组合实际检查MIME/base64及同图字节。fixture只增加每次请求body队列，没有新验收工具。所有图像、文本、身份和地址均合成公开数据，没有模型或真实截图。

| 验证项目 | 离线实际结果 | 原生/视觉质量 |
|---|---|---|
| text_only保持风格；mixed/visual/uncertain解释；模式覆盖与3版再生成 | Pass，实际双协议/请求计数与同图检查 | 分类语义与译文质量NotRun；8×8合成像素不是图像质量样本 |
| 非法/缺字段/超限/不可读；完整JSON但缺协议终止 | Pass，只有一次HTTP，无final/JSON正文 | 真实模型输出/收费行为NotRun |
| 分类A排队后换图、final排队后Clear、phase边界取消/隐藏/锁屏/设置/绑定变化 | Pass，实际canonical receiver/HTTP/job | WPF、原生采集、COM和取消时限NotRun |
| 停止首个final delta、清理异常、owner释放与显式再生成 | Pass，受控托管像素和真实任务 | 系统副本/原生位图物理擦除NotRun |

运行顺序全部保留：r01定向100/100；r02完整1107/1107；r03 App0W/E。独审发现启动异常：Gate先装新identity再取消旧CTS，旧取消回调抛错时新Job尚未记录，原Clear只能停旧id。r04用真实Gate回调复现，**2通过/1失败**，失败是无job新identity仍running；两协议缺终止用例已通过。修复只在组件TryStart异常路径结算实际out新identity，原错/清理错聚合，不改Core。红版生产/测试原字节另存。r05修后115/115；r06最终1118/1118；r07最终App0W/E。

12份本批SDK log/TRX和2份红版源码原件均唯一命名、精确-text保全，父91份原始回执不变。源码manifest、实际计数、所有原件hash、依赖与阶段边界见[固定证据](../test-evidence/2026-10-15-screenshot-image-routing.json)。没有覆盖旧回执，历史覆盖前字节仍Unknown且未重造。

250/600ms、来源/密码/完整性/取消/清理门和生产离线复用Off保持，既有双协议序列化/提示词/安全Core不改。每次dotnet前export且命令前显式`DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；证书存储/信任Unknown，不读库/配置/trust/删除。Actions不查询不调度不绕Forbidden、远端run数Unknown。无main/强推/部署/新费用凭据权限协议、真实模型或外部内容加载；网络仅常规Git与合成loopback测试。
