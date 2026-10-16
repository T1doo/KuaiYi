# ImageTransform 贴边坐标与既有单位 bbox 边界

父版本 `1b658b598f96f4af1790411ec1b68774a6e80be8`，正常分支 `dev/image-coordinate-boundaries`；唯一写入者 root，inline_review 只读独审。默认 Git 网络核远端父版及 main，单 worktree、干净基线与祖先关系成立；不恢复旧实例。

## 修复与数值合同

给定 `PixelRect(-715,-923,-615,-873)` 与 `OriginalImageRectangle(.1,0,99.9,50)` 现可 Original→Normalized→Desktop→Normalized。原实现只平移起点却携带局部宽度，反向减原点的消去误差使同一贴边框被错拒。现分别平移左右/上下端点后重建宽高；桌面输入先在物理 crop 空间检查点及 `width <= Right-x`、`height <= Bottom-y`，再减原点并映射端点。点入口也先验物理边界，防止跨零 crop 将 `BitIncrement(1)` 减负原点后舍入成合法端点。

像素空间继续使用有限坐标及严格剩余宽高合同，无 epsilon。若已验证宽度恰好等于 `limit-start`，该单次减法确定其贴边端点；统一 End 恢复 limit，不要求不可保证的 `start+(limit-start)==limit`。其余合法输入才允许 min 吸收端点加法舍入。输入不靠 clamp 获得资格。外一档宽高与四边物理点立即邻值均拒绝；同轴 identity 直接保值。浮点往返仍受物理坐标可表示精度限制，测试用实际桌面 ULP 推导误差界，不宣称任意双精度值无损。

同时修复父版之前已存在的 ImageClassification `.8,0,.2,1` 错拒。这是另一项单位坐标合同：共享 Core UnitInterval 取**已经解析为 double 的最短往返十进制 R 表示**，内置 BigInteger 精确对齐后判断和是否不超过一。有限 [0,1] double 的有效位和指数固定有界，无新依赖；Transform 的 UnitImageRectangle 与分类 parser 共用，物理像素空间不使用此合同。

此单位合同允许 `.8+.2`，拒绝 `.8+BitIncrement(.2)`、`.9+BitIncrement(.1)`、`.7+.3000000000000001`、tiny+1，以及 decimal 28 位不足以识别的 `.9999999999999999+1.0000000000000001e-16`。有意收紧 `.7+(1-.7)`：后者 R 值为 `.30000000000000004`，十进制和超过一，虽二进制加法可舍入成一也拒绝。这不是二进制精确和、原始 JSON 任意精度审计或所有旧有限 pair 的兼容承诺。分类其余字段/上限/完整协议终态及正面积要求不变。

## 实际执行与失败

所有 dotnet 激活前后及执行前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，Release/no-restore；App 加 EnableWindowsTargeting。实际生产 Core helper、Provider parser 与链接的 Imaging 源码被执行，不以替代几何实现证明生产行为。

| 回执 | 结果 | 说明 |
| --- | --- | --- |
| r01 | 0 通过 / 2 失败 | 原桌面往返及旧 bbox 错拒；两份原生产源码和初始测试原件保全 |
| r02 | 101 / 101 | 初修与既有分类/归一化专项 |
| r03 | 170 通过 / 2 失败 | 新夹具错误地要求跨零浮点加法恒等；实际 `top+height` 舍入为 `854.00000000000011`，四份当时源码保全 |
| r04 | 172 / 172 | 增加精确 End；r03 的不可保证加法等式改为精确最大残差等式，仍保持 20 轮贴边、ULP 界及外邻值拒绝断言 |
| r05 | 1379 / 1379 | 父版 1306 个精确测试名完整保留，新增 73 项；无跳过 |
| r06 | App 0W / 0E | Linux 上 Windows 目标交叉构建，不是原生运行 |

新增 73 项含 24 组正负/混合/大原点与两向缩放 crop、每组 20 轮右下贴边往返、四边点/矩形立即邻值、跨零消去反例、共享单位边界及分类正面积规则。11 份 SDK log/TRX 与 7 份红版源码按原字节复制到唯一 `-text` 路径，父 162 份原回执及 1745 份既有证据不改。另一次只读 traceability 检查误用 `tasks` 键报 KeyError、零变更，改读实际 `items`；未重造该工具输出。

r03 的两失败均归新增夹具，不据此声称旧 min 已导致往返失败；独审按旧 min 重算修正后的 24×20 矩阵也通过。End 是对已知端点语义的明确完善。独审另一次只读误猜 App 日志名得到 ENOENT，按 rg 列出的实际 r06-app-final.log 核准，无 SDK/写入/网络影响。

固定清单、精确回执哈希、独审与父文件保持检查见 [证据](../test-evidence/2026-10-16-image-coordinate-boundaries.json)。未重跑完整 Core.Tests 项目、CMake 或 Windows 探针。

## 阶段与边界

不改变截图规范化/可读性策略、请求编排、usage、缓存、原图所有权或原生 UI；普通截图仍本地未翻译未发送，生产离线复用 Off。250/600ms 与来源、密码、完整性、取消、清理门保持；原 Core 文件、全部 App、其余 Provider/Imaging、8 模板/8 workflow/43 锁保持。

P6-02 仍 InProgress/Blocked，P6-04/05 仍 InProgress/NotRun。多格式、EXIF、隔离解码、Skia 版本/许可证/样本、OCR、一般缩图文字可读性及视觉质量尚未完成。真实 WPF 测量/选择/阅读位置、Windows/COM/截图/取消时限与完整 P2/P4/P6/I/J 仍 NotRun。旧证书存储/信任及旧覆盖字节 Unknown，不读改重造；Actions 不查、不调、不绕拒绝，run 数 Unknown；无 main/强推/部署/新依赖费用凭据权限协议/真实模型或 Windows CI 实验。
