# P6-03 本地 OCR 预览与显式文字发送

父提交 `30425cc03130f3fa9ecc94777a54aac64219fd71`，开发分支 `dev/ocr-only-confirmation`。实际文件核查确认：P0 仅本地 OCR，正常 Shell 仅保留截图预览，尚无完整 OCR→文字调度。本批接正常截图 owner 的原尺寸白底 PNG→原 ImageWorker/NativeOcr→本地只读文字预览→显式确认→既有 TextTranslationCoordinator。四风格冻结到请求快照，不改全局设置；目标来源和模型在确认前显示。文字模型不收到 PNG、base64、MIME 或图片内容块，不接视觉分类，不恢复非法分类的自动降级。

预览显示原句“当前模型不支持图像理解，图形关系未分析”，版本来源标签及提示保留 OCR-only 限制。语言是用户明确选择的本地语言标签；缺系统能力、包身份、语言、本地组件或取消均停止，零 HTTP，不安装/下载/重试。没有置信度或图形理解成功声明。窗口属于专用 OCR-only 功能，不能代表取词、动效、图文理解或 P0 平台验收通过。

`OcrOnlyCoordinator` 位于实际 Imaging DLL，无 Provider/HTTP 依赖。预览绑定完整 canonical 请求身份、原图 SourceId、所选版本、设置版本、语言和一次 PreviewId；先确认原 Worker/IO/进程终结才能批准。OCR 来源具有明确空桌面身份及独立 provenance，只有当前已批准对象的引用可进入 TextCoordinator，默认 OCR 回调拒绝。普通 UIA/Clipboard/OleDrop 保持原排除和来源检查，非 OCR 不能携带 OCR provenance。OCR 首发及再生成始终显式确认；同图文字可在 PNG 正常释放后保留三版本，按当前设置再翻译也保持原受信来源对象。

所有嵌套锁按 Gate→局部状态顺序；先执行外部来源证明，再复核命令修订/原引用/身份/epoch/设置，防止回调 Clear 后恢复旧文本。Worker 使用原局部 owner，旧 ClearAsync 只等待捕获的原任务并终结该 owner，不能操作后继 C。App 异步返回后还重验所选原图引用/版本、On/可见/非暂停及启动后 epoch，失效时不覆盖新来源提示；窗口计时器与异步继续段捕获原协调器，字段引用变化和退出时拒绝旧 UI 操作。非协作原 Run 返回前保留借用 PNG；未确认清理保留 worker 且拒绝新 OCR，Clear/退出报告未确认。OCR取消和对话窗关闭分别尝试并汇总异常；原清理在Attempt/finally中继续，不因新hook抛错而跳过。Off、锁屏、隐藏、换源、清空和设置/模式变化隔离迟到预览；正常确认交接仅清预览，不撤销本次已批准文字来源。

`ImageWorkerClient.ValidateOutput` 先检查坐标不超过宽高，再严格比较 `Width <= imageWidth-X`、`Height <= imageHeight-Y`。给定 1×1 图、X=1/Width=double.Epsilon 及纵向对应红测均失败后修复。无 epsilon 容差，保留零 extent 和精确剩余宽高合法框。

| 实际执行 | 回执 |
|---|---|
| r01 坐标红测 | 0 通过 / 2 失败，生产漏检；修前 Client、测试源码及 log/TRX 保全 |
| r02 / r03 / r04 | 2/2、46/46、46/46；后两轮分别为初版和审查锁序/owner 修复 |
| r05 夹具编译失败 | 3 诊断、0 执行、无 TRX；误用 SettingsSnapshot 字段/枚举及未赋值字段，修前源码与原 log 保全 |
| r06 来源证明重入红测 | 1 通过 / 2 失败（admission、approve）；生产及测试原源码与 log/TRX 保全 |
| r07 / r09 | 59/59、62/62；增加来源重入、同/新设置再翻译及旧 Clear/新 owner 检查 |
| r10 / r16 / r17 完整 Provider | 1478/1478、1478/1478、最终 **1481/1481，0 失败、0 跳过 = 父级 1416 完整保留 + 本批 65**；测试名多重集逐项核对 |
| r08 / r11 / r12 / r13 / r14 / r15 | App、Worker、四次最终 App Release 交叉构建均 0 警告 / 0 错误 |

成功 OCR 全部是明确标识的合成合同：实际 Imaging/Provider DLL、真实本地 Kestrel/HTTP、双协议×四风格纯文字请求；另3项直接链接Shell生产代码并以抛错OCR hook/WPF端口证明取消异常不能跳过原状态/图像清理，保留原fallback owner；验证拒绝确认/能力缺失/取消零 HTTP、错误输出、边界、来源/设置/模式/Off/锁/隐藏、排队迟到、借用缓冲、未确认清理、清除重入、C 所有权、三版本和同文不同图。Linux 实际托管子 Worker 按配置和 SHA256 绑定，沿原 NativeOcr 只得到 `UnsupportedPlatform`。**未执行成功 Windows OCR**；App 真实窗口、模态路由/焦点、测量/选择/阅读锚点和取消时限均 NotRun。完整 P6/I/J、P2/P4/P0 仍未通过。

固定证据见 [JSON](../test-evidence/2026-10-16-ocr-only-confirmation.json)。每次 SDK log/TRX、红/编译版源码和核验记录复制到唯一原件路径，以 `-text` 保留原字节。父级 219 原回执、1805 既有证据、43 锁、8 workflow、8 模板保持；所有第三方版本/锁及项目引用不变。一次保全脚本误解析 Git 引号路径失败，改用 `-z` 后完成，不重造原件。其他只读路径误猜完整记录于 JSON。

生产离线复用 Off；250/600ms、原 Worker 15 秒和来源/密码/完整性/Job/process/取消/清理门保持。所有 dotnet 激活前后、命令及新测试子进程显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；旧证书存储/信任 Unknown，不读库、配置、trust 或删除。Skia 四包显式许可未获答、源码 403 门保持：不 restore/install/load/execute 或绕拒绝。Actions 不查询/调度/绕拒绝，远端 run 数 Unknown；无新依赖/费用/凭据/权限/协议接受/真实模型、Windows/CMake 探针、新 CI、main 修改、强推或部署。
