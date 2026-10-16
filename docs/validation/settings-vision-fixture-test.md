# 设置内置图视觉测试：本机 fixture 批次

父 `444c2dd5dc166e26a6edab7c6eba8d81df66e909`，tree `4f136a65c23097760da818abcc51f23ce456cf4d`。在原环境核文件、SPEC/AGENTS、clean Git、唯一 worktree/root 写入者、默认网络远端和原250祖先后建立 `dev/settings-vision-fixture-test`。原 UNKNOWN 实例不恢复；`vision_probe_review` 仅只读独审，无 SDK、网络或写入。

SPEC §8.3 的设置页现在有独立“测试文字连接”和“测试视觉能力（仅本机 fixture）”按钮。本批只在原显式开发启动边界内工作：固定 loopback 地址、固定 TEST_ONLY_FIXTURE 模型、AuthNone、无密钥/自定义头，不调用真实模型。普通保存的远端配置在准备阶段明确阻断、HTTP 为零；不声称真实视觉服务已验收。确认页列出固定样本、端点、模型及本地合同限制，用户必须单次确认。视觉专用 identity 只授权精确端点和协议，与文字测试、普通生成身份分开；最终 canonical send 再检查完整身份、目的、内容和配置。

图为仓库已有公开位图字形样本的逐字节副本，2034 bytes、640×160、PNG RGB8，SHA256 `21b267376fd764e726d41daa71e32526d6f513cf1343f26f5947008509a15b8d`。独审核 IHDR/IDAT/IEND、CRC、像素及无私有元数据；以 EmbeddedResource 包含，读取时核长度及摘要，每次给独立副本。调用者数组在 provider 验证前再取私有快照，迟到修改不进入实际 wire；没有图片路径、截图、剪贴板、解码器或 Skia 依赖。固定 system 不给出图中答案，未以服务复述题目当作识图证明。

文字测试继续只接受原固定文字且拒图。专用视觉目的仅豁免固定图的 capability 前置判断，普通图片的 Unknown/Unsupported 阻断保持。请求仍经过原配置绑定、Settings-only 暂停、Off、取消、总预算及 connect/header/first/idle 有界读；视觉测试最多一个 HTTP，429/503 也不自动重试。结果不写 capability、不启用 On，不开放截图/离线生产复用。

结果分行显示认证（AuthNone，未验证）、连通/协议、普通输出、流式、视觉和服务错误。Stream=true 可能收到 JSON 回退，因此只显示“已请求流式；实际响应形式和流式能力未知”；非流式显示未测试。空输出不冒充普通输出成功；图像理解始终 Unknown，实际文字与 usage 可检查但不推断质量、费用或计费完整性。

设置按钮处理函数拆到独立 partial 源码，测试项目只链接该生产文件并替换 modal/control/data 端口。生产窗口仍使用原 WPF MessageBox/控件；不声称 Linux 执行了真实 WPF。模态确认前占有命令槽，防重入发送；编辑、换配置、清密钥、取消、关闭会撤销本意图；实际 Closed 才永久撤销视图，保存任务暂时取消 Closing 不锁死按钮。工作流只接受准确、未消费的 Prepare 对象；外部 settings 回调后复验修订与 canonical identity。安装 B 后旧 A.Cancel 抛错也只结算 B，保留原错及可能的清理错，不能清掉回调新建的 C；finally 同样不覆盖 C。

| 原始运行 | 实际结果与失败保全 |
|---|---|
| r01 | 新测试端口未显式初始化可选回调，CS0649；0 执行、无 TRX。保精确源码/log，显式初始化为 null |
| r02 | 新后台测试两个异步启动未显式观察，CS4014；0 执行、无 TRX。保源码/log，改为 `_ =` 并继续由存储 Job 等待实际结算 |
| r03 | 新隔离夹具写错已有 enum 名，且 xUnit 选中已禁用 async overload；0 执行、无 TRX。保源码/log，改已有状态名及同步 Action 包装 |
| r04 | 117 项，115 通过 / 2 失败。新锁屏夹具错误调用 SetSuspension(SessionLocked)，生产明确要求 SetSessionLocked；保精确源码/log/TRX 后改实际 API，无生产门控弱化 |
| r05 | 169/169：新120项和旧设置/API49项，0 失败/跳过 |
| r06 | **1857/1857 = 父1737全保留 + 新120**，0 失败/跳过；实际 testName 多重集比较无父项缺失 |
| r07/r08 | App、Worker Release 交叉构建各0警告/0错误 |

新120项：VisionFixtureWorkflowTests56、VisionFixtureIsolationTests34、SettingsVisionFixtureTests30。两协议×流/非流×三种能力均走实际 Kestrel socket、生产 serializer/parser、receiver、workflow；检查精确 PNG/MIME/base64/system/source、目的/端点/协议、无密钥/额外头/非固定字节、无授权零 dials、请求数组突变、429/503 单请求、普通图片/文字边界、配置/版本/只读/恢复/锁屏/取消/Off、排队迟到、冻结 header/first/idle/total（含仅时间戳过期），以及安装/发送证明回调重入和异常 B/C 隔离。UI 命令测单次确认/拒绝、模态重入/关闭/编辑、未保存/待密钥/无边界、两按钮忙互斥、实际 JSON 回退、空输出、结果/用量及按钮恢复。

[固定证据 JSON](../test-evidence/2026-10-16-settings-vision-fixture-test.json) 包含摘要、原始回执精确副本和来源、执行二进制、测试名连续性与父文件保全。28 新原件（12 父源码/项目、4 失败源码、8 SDK log、3 TRX、1 preflight）以精确路径 `-text` 保存。父累计318原件及1908旧证据逐字节不变；全部父测试源码不改，PhaseHttpFixture 仅增加可控 status fixture，原默认200路径保持；Provider测试项目仅多链接1实际命令文件，Providers项目仅多1 EmbeddedResource，不加引用/包/锁。43锁、8workflow、8prompt原字节保持。P4-01/F07/F10/F11仅追加本批证据，整体状态和全部P6不改。

真实 Windows/WPF 窗口操作、布局/选择/阅读位置、OCR/COM/原生 Job、250/600ms、模型视觉质量、证书存储/信任及完整阶段仍 NotRun/Unknown。基本表格、代码及表格局部横向滚动已在更早批次接入生产代码与便携测试，真实 WPF 横滚/排版/选择/阅读锚点仍 NotRun（更正此前误沿用“缺口”说法；旧 JSON 的 BasicMarkdownTablesAndCodeHorizontalScrollRemain 字段保留为历史记录，当前状态见本句和后续更正说明），生产图片复用 Off。所有 dotnet 激活前后与命令显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；不读改证书库/配置、不 trust/删除。Skia 许可待答和源码403不绕过，不 restore/安装/加载/执行；Actions不查调，run数Unknown。无真实模型、费用、凭据、权限、新协议接受、main修改、强推、部署、Windows/CMake探针或新CI。
