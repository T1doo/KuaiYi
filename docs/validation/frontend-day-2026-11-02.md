# 2026-11-02 快译前端日：实现候选与验收边界

后续源码片与资源纠正见 [followup报告](frontend-day-2026-11-02-followup.md)。本文件中的340通过仅属于80dcecd；后续文本倍率/独审修正未构建、未测试，不能沿用历史通过。

本轮为实际 .NET 10 WPF/Win32 源码修改，尚未宣称整份文档全部实现或原生验收通过。产品基准为 `2a15c0ee7d057a468859f58ee603b5d24a2877da`，新分支 `dev/frontend-day-20261102`。原 writer 已停止产品写入；本环境没有 `.agents/skills`，已读取实际 `AGENTS.md`。原 main 是 `51d077481cf6303602fcf5d2718213da9cf05178`，为交接开发基准祖先；原开发历史领先 main 406 笔，未合入 main。

## 文档与授权范围

- Library `libfile_cc9dd52965108191a18f019d1b048b2a`，version 1。通过当前 Library `prepare_materialize` 与本次取得的官方 helper，在本消费者环境下载，未借用父环境路径或历史错误。
- 原 DOCX 4,624,153 B；SHA256 `cfe55dbc74e34b02da650e24e943f375048bfa5e96606d6ca5619288f5645a14`。879 个正文段落完整读取，47 个实际内嵌图片文件逐组查看，覆盖39图组；原作与方案像素身份区分。未把参考图装入产品。
- 私人路径/令牌模式扫描未发现命中，外部关系均为 HTTP(S)，逐图未发现个人截图。此检查针对正常提交设计稿，不授予第三方资产的再分发许可。
- 仅此 DOCX 已提交并推入 main：[80a726afc101035ec051dc004445743288ffa613](https://github.com/T1doo/KuaiYi/commit/80a726afc101035ec051dc004445743288ffa613)。该提交只有 `docs/design/快译前端设计修改方案.docx`。推后正常 fetch 并 `git show` 读回原文件，SHA256相同。
- 开发分支从交接准确 SHA 新建，只 cherry-pick 文档，不合并整个开发历史至 main。无强推、部署、Release、真实服务调用或用户电脑操作。

## 本轮源码结果

| 范围 | 真实接线 |
| --- | --- |
| 中性主题与密度 | `DesktopPalette.cs`、`DesktopTheme.cs`：白/深色实底、细控制缘、可辨焦点/错误、6 DIP主体和3 DIP控件。移除原暖橙资源和浮窗径向动态洗色；Off/On红绿不变。按下态对比不足的首轮失败保留，修底色不降断言。 |
| 控制缘 | `OverlayReadingToolbar.cs`、`OverlayPresentation.cs`：本版风格、停止/重新生成同位、复制、收起、设置、更多按频率安排；窄空间优先停止/收起/更多。触控命中区44 DIP且互不重叠，主锚点48 DIP；等待状态保持停止直达。 |
| 阅读面和来源 | 元数据改在正文之后；版本栏预留28 DIP避免挤压正文。`MarkdownFlowView.cs`短标题16/15倍率，正文仍1.55行高；来源内显示所示版本模型/发送origin，并有“复制原文”独立入口。代码、表格、图片、零请求折展与阅读跟随继续用原实现。 |
| 复制 | `Tag`先稳定，才把Content切为“已复制/未确认”。仍只在实际OS调用成功后报告成功；确认反馈1.5秒复位，版本/内容/隐私变化清旧反馈，旧成功计时器不能抹掉后来的失败。无自动复制重试。 |
| 版本与风格 | 专用两行三版本短菜单，状态、风格、模型与选择勾保留。所读版本的实际风格不会被待选风格改写。普通文字再生显式 `useSessionStyle:false`；“按下次选项生成”显式true；全局重译仍独立入口。原Provider默认行为向后兼容。图片复用资格门仍false，不借本次样式工作开启。 |
| 设置七页 | 外壳860×660、原192导航、24留白、小工作区原适配与固定底栏框架保留；删除大底色卡与营销式头部。常规/集成不伪报能力；图文混合默认解释接回既有草稿字段。快捷键按普通分隔行重排，错误状态对应动作。 |
| 字段与错误 | 常驻label、模型手填、配置名称键入定位保留非editable Combo的明暗模板；高级参数改就地折展。验证错误显示邻近提示、打开高级区、切页并在保存结算后定位首错；失败保草稿。UI宽度320–600与实际有效范围一致，旧数据格式仍可读。 |
| 安全和状态 | 原启动Off、独立回看、停止/收起/隐藏区别、请求身份、三版本淘汰、受限Markdown与端点同意均保留。未新增服务、权限、凭据或持久历史。 |

## 全文逐节覆盖

| 文档节 | 实现或保留位置 | 状态/限制 |
| --- | --- | --- |
| 1–2 参考结构及淘汰外观 | DesktopPalette、SettingsSections、OverlayPresentation | 新结构候选；未复制品牌资产 |
| 3 单阅读面 | OverlayPresentation、MarkdownViewport | 已改，实际WPF像素NotRun |
| 4 上下生长 | 原OverlayFrames/OverlayGeometry；新原生圆角24→6 | 几何合同保留；HWND命中Blocked |
| 5 控制频率 | OverlayReadingToolbar | 逻辑测28/44命中；Windows触控NotRun |
| 6 控件状态 | DesktopTheme | 焦点/悬停/按下分离；颜色数学与原生分报 |
| 7 独立回看 | 原OverlayResultFlow、350/250ms计时器 | Off零请求与取消语义保留；实机NotRun |
| 8 复制 | OverlayCopyPresentation、稳定Tag | 生产命令端口回归；OS剪贴板NotRun |
| 9 三版本 | OverlayPresentation、OverlayResultLabel、原OverlayResults | 缓存保留策略不改；Popup像素NotRun |
| 10 风格三层 | TranslationFlow、TextTranslationCoordinator | 新显式入口，原HTTP Mock检查快照/请求计数 |
| 11 中文和来源 | MarkdownFlowView、MarkdownSourceCopy、OverlayMarkdown | 原文复制与实际来源元数据接线；WPF排版NotRun |
| 12 图片 | 原截图/图像预览和发送门 | 原生截图、OCR和视觉理解未新增资格 |
| 13 错误 | OverlayFailureNotice、DesktopTheme.Error | 持久局部两行提示与恢复入口；不遮旧结果 |
| 14 动效 | 原OverlayFrames/OverlayFrameClock | 220/180/80原合同不改；新真实帧证据NotRun |
| 15 材质 | 主题实色默认、短菜单样式 | 可选Mica/Acrylic不强启；无透明大墙 |
| 16 主题字体 | DesktopTheme、MarkdownFlowView | 系统字体，无字体下载；高对比实际读屏NotRun |
| 17 设置外壳 | SettingsChrome、原自适应Panel | 七页/保存取消保留；像素和极小工作区未验 |
| 18 API字段 | SettingsWindow、SettingsApi、SettingsValidationPresentation | 草稿、端点、手填、短显与高级首错；真实连接不运行 |
| 19 外观设置 | SettingsWindow | 有效宽度/字号/明暗/减少动态/三内存版说明 |
| 20 风格、常规、集成 | 原模板编辑与组合预览；普通分隔行 | 启动项只显示保存状态，资格未闭合不能启用 |
| 21 快捷键、隐私 | SettingsHotkeys、原ClipboardPermissions/Diagnostics | 真注册/导出/清除边界保留；原生NotRun |
| 22 焦点、大字、触控 | 原自适应设置；新44 DIP控制命中 | 独立200%文本缩放接线尚未完成，不能声称大字验收通过 |
| 23 令牌尺寸 | DesktopPalette、DesktopTheme、OverlayPresentation | 原48锚点保留，圆角同步原生ApplyBounds |
| 24 动作矩阵 | boundary-coverage.csv B01–B12/B68 | 代码与端口证据分列，非全部Windows通过 |
| 25 输入图片 | B13–B24/B69–B70 | 不下载图片/模型，不打开原受阻路径 |
| 26 错误恢复 | B25–B35/B71–B73 | 原有限重试与身份门保留；局部错误呈现已改 |
| 27 时序资源 | B36–B51 | 全部原生性能目标NotRun，不把UIA250/600当动画 |
| 28 全设置字段 | B52–B58/B74–B76 | 普通表单改动；原资格受阻项分别保留 |
| 29 原生边界 | B59–B67/B77 | Windows焦点/DPI/命中/锁屏/读屏Blocked |
| 30 实际文件族 | 实际src修改与测试输入manifest | 无React壳、占位主路径或假截图 |
| 31 验收18项 | acceptance.csv | 每项Expected和Actual、环境/倍率分列 |
| 32 K01–K24 | design-traceability.csv | 逐项追溯，旧暖白灰绿审美不强留 |
| 33 图册26项 | design-traceability.csv | 逐项取舍，原图册不覆写 |
| 34 来源与许可 | 原DOCX、document-review.json | 参考评论与产品可打包资产区分 |

覆盖CSV取第24–29节67条表格边界，加10条明确正文合同，合77个本轮检查项；不声称原文用B编号，也不以条目数量代替验收。

## 验证与证据

当前环境为Linux、新容器，未恢复原Windows容器或原artifact目录。安装的是SDK10.0.401，仓库global.json锁10.0.402且禁止rollForward。没有修改锁或下载整套SDK。所有本轮构建从目录外调用已安装SDK401，属于补充交叉编译，不能冒充固定SDK402正式构建。

- App最终交叉构建日志：`build-app-final.log`；0警告0错误。Windows App没有运行。
- Provider测试项目先完整构建，后仅本项目 `--no-restore -p:BuildProjectReferences=false` 编译更新，以使用已建引用并避免重建/下载Win32图像包。最终命令、DLL和测试结果以evidence.json为准。
- 首轮 `frontend-r01.trx`：171项，169P/2F/0skip；两失败为浅深按下态对比；未删除原失败证据或降低断言。第二轮同340集合337P/3F，三项为旧20 DIP状态栏/大标题尺寸断言；按新版28 DIP版本栏与16/15短标题比例更新，所有有界、节点保留断言保留。最终同340名称集合340P/0F/0skip，原失败另列，不与首轮相加。
- 定向测试为原生产partial+Linux控件/剪贴板端口、纯布局及本机Mock HTTP。不是WPF事件、真实系统剪贴板或付费模型运行。
- 原停止逻辑测试的快照断言从原长状态行移至完整来源披露ToolTip，内容断言保留；另测短状态、复制标签和待选/本版/全局三个生成语义。
- 原测试、构建日志、560输入SHA清单、DOCX及47图哈希、两类覆盖CSV一起归档。实际源码未替换为Mock UI。

## 资源偏差及剩余工作

本轮一次Provider依赖恢复展开缓存约611 MiB，超过继承的423,501,153B剩余内容额度。这是本轮资源控制失误，不能因新环境有30GiB空闲就重置额度。发现后停止新增下载和打包，移除的仅为本轮新建 `/workspace/kuaiyi-validation/packages/skiasharp.nativeassets.win32` 可重建缓存（28文件/409,589,222B），没有删除原保护文件或改依赖锁。真实过程日志和resource-correction.json保留；后续只用已有DLL、其他已缓存引用进行验证。最近已知本轮输出观察约394,949,043逻辑B/398,004,224allocatedB；这只是已知生成根，不是完整峰值上界或原保护树重哈希证明。

原5,894文件/918,676,127B保护范围不在这个新容器挂载中，没有将父环境路径当共享，也没有宣称本轮核过其哈希。原Core累计900秒的交接余额上限622.532秒不重置，23项Core仍未验；本轮没有运行该集合、UID-map路径或自包含打包，7GiB门不放宽。原Windows窗口和CI不重跑；现有workflow均精确分支/路径过滤，不匹配本分支，无新CI预算消费主张。

尚未完成：独立200%文本缩放的完整接线与重排；真实WPF同viewport前后截图/窗口像素和独立审查；混合DPI、原生命中、焦点、UIA/OLE、系统剪贴板、截图与资源采样；固定SDK402正式构建；原安全安装来源、签名、生产pins和图片复用资格。少数设置集成功能仍受原资格门关闭（含开机启动），未画成可用开关。以上分别为NotRun/Blocked，不把整份DOCX标为完成。
