# 快译前端接续：文本倍率、自适应与资源复核

本片基于 `80dcecd84f88cb245de662c26c5208f107482381`，仅交付源码候选及只读独审。**没有运行本片的构建、测试、Windows或预览。旧340/340仅属于80dcecd，不外推到新源码。** main中的DOCX提交仍为 `80a726afc101035ec051dc004445743288ffa613`，原字节不变；产品仅在 `dev/frontend-day-20261102`。

## 资源账与冻结

上一份观察漏计了NuGet HTTP缓存，所以约394,949,043B不是完整当前占用。本轮只读复核保留的包体、元数据、生成目录，结果如下；完整文件大小、SHA256、mtime与分组在 `resource-accounting-followup.json`。

| 项 | 精确已知值/边界 |
| --- | --- |
| 原累计内容额度 | 1,342,177,280 B |
| 原保护内容/数量 | 918,676,127 B / 5,894文件（继承报告，非本轮重哈希） |
| 继承剩余内容额度 | 423,501,153 B；不因新环境或清理重置 |
| 17个完整依赖包响应体 | 139,440,484 B |
| 已保留HTTP元数据响应体 | 814,945 B |
| 上两项完整响应体合计 | 140,255,429 B；不是含重试、头、TLS和压缩的实际网络总流量 |
| DOCX完整响应体 | 4,624,153 B；与依赖分开 |
| 完整网络下载总字节 | **未知**；未捕获传输计数/完整重试日志，Git fetch传输字节也未知，不伪造精确总数 |
| 当前已知生成根逻辑字节 | 至少536,254,916 B，含此前漏计HTTP缓存 |
| 当前已知生成根分配块字节 | 539,418,624 B |
| 内容余额 | **0 B**；已知超额至少112,753,763 B |
| 完整峰值/全局足迹 | 未建立；上述下界尚不含Git pack/checkout、新tracked证据及其他未知根 |
| 原Core诊断账 | 原900秒，交接余额上限622.532秒；23项仍未验，本片不运行该集合 |
| 自包含打包门 | 原7GiB门不变，不打包 |

没有保存restore前HTTP-cache文件清单；mtime和完整包体也不能证明每个字节都在本任务重新越过网络。因此上表是精确保留体积，不冒充可追溯的新增下载总量。

“恢复缓存约611MiB”是当时 `du` 展开后分配空间的近似观察，不是下载量。原数字内容额度是存储/内容账，不能拿包体传输字节相减来算网络余额；未记录另一个可代用的数值网络上限。

已发生的唯一清理为本任务新建 `/workspace/kuaiyi-validation/packages/skiasharp.nativeassets.win32`：原删除记录28文件、409,589,222逻辑B，目标现已不存在。验证目录由本任务新建，restore日志明确该版本，HTTP缓存时间与本次restore对应；它不是旧保护目录。仍保留该包86,914,944B的HTTP原包和校验，未解包重建。精确回收的allocated blocks未在删除前采样，**未知**；不把逻辑大小当实测回收空间。下载/内容历史不记返还、不归零。之后没有再次清理。

保护文件本轮**仅未修改/删除，没有逐哈希复核**；原保护树未挂载在本消费者环境。父报告的11月1日21:47核验不是本轮新证明。

冻结持续：不下载依赖/缓存/SDK/大包，不restore/build/test、不扩盘、不自包含打包、不重启旧Windows/UID-map或23项Core。只继续已授权的小规模源码、测试源码、文档与普通dev Git提交/push。新源码虽可审阅，不能因此取得执行或原生资格。

## 独立文本倍率接线

- `NativeTextScale.cs`使用系统 `UISettings.TextScaleFactor` 的只读IUISettings2 ABI，不引新包、不猜注册表值。RoInitialize成功（含S_FALSE）才平衡RoUninitialize；QI、接口引用和HSTRING在finally释放。原生失败返回Known=false，沿用上次有效呈现倍率；初始1仅为回退，不当作读取成功。
- 文本倍率与DPI分开：正文12–22用户字号再乘系统倍率；普通字段、显式文字字号和行高绑定独立Factor。没有整窗LayoutTransform，48 DIP锚点和Win32物理坐标不乘文本倍率。
- 控制缘按实际倍率重新留文字空间。125%也进入响应策略；放不下时次级进入更多，必要时用完整内容宽度的额外行；停止、收起、更多不因缩字来凑。高度纳入同一个新LayoutPlan的测量及视口扣除。工具条真实高度在动画结束、移动和重锚时保留。
- 状态/版本栏、错误条、来源metadata和复制入口随倍率保留空间。版本创建序号与缓存数量分开，避免“版本4/3”。无缓存的回看、复制和再生初始禁用；回看仍取缓存，Off不暗启用。
- 窗口WM_SETTINGCHANGE及UserPreferenceChanged只排队到原UI线程；入队前合并，关闭后忽略待投递并退订。**未直接订阅IUISettings2.TextScaleFactorChanged，实际Windows通知覆盖尚未验证。** 不能宣称系统文本设置的所有实时通知已通过。

原生属性语义见 [Microsoft UISettings.TextScaleFactor](https://learn.microsoft.com/en-us/uwp/api/windows.ui.viewmanagement.uisettings.textscalefactor?view=winrt-26100)。ABI由独审按 [Microsoft windows-rs的IUISettings2声明](https://raw.githubusercontent.com/microsoft/windows-rs/master/crates/libs/windows/src/Windows/UI/ViewManagement/mod.rs) 核对；初始化/引用规则另见 [RoInitialize](https://learn.microsoft.com/en-us/windows/win32/api/roapi/nf-roapi-roinitialize) 和 [Marshal.QueryInterface](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.marshal.queryinterface?view=net-10.0)。这些是声明与设计依据，不是运行证据。

## 独审发现及源码闭合

安排一名只读审查者 `/root/readonly_frontend_review`；单writer、同时仅本writer和该reader。审查者没有改文件、运行构建/测试或下载依赖/缓存。完整分项见 `readonly-review-followup.json`。

| 发现 | 本片源码处理 | 验证层级 |
| --- | --- | --- |
| 原文复制仅看前后bool，撤销再恢复可能误认成功 | 原epoch/结果revision/所选版本guard与copyAttempt；写后必须仍为原资格 | 源码复读；系统剪贴板NotRun |
| 非当前API配置的首错可能指向当前配置 | 逐配置独立验证保留ID，先选回对应profile再定位字段 | 源码复读；WPF草稿事件NotRun |
| HTTP回环错误定位到远端不安全开关 | 按端点IsLoopback选择对应checkbox，并保邻近错误 | 源码复读；运行NotRun |
| 保存/取消会随长反馈滚走 | 操作Dock.Bottom固定，仅反馈滚动；先有限宽/无限高测动作，再确定并复测footer | 最后指定范围独审无阻断；实际极小工作区NotRun |
| main横向Auto导致偏好行按无限宽测量 | main恢复有限宽；标签/输入按真实宽堆叠，局部代码/表格保原有界横滚 | 源码复读；长文本布局NotRun |
| 菜单长model ID/大字无边界 | owner显示器rcWork映成DIP，限制宽高、标题换行及保留滚动；Factor绑定owner，箭头用固定矢量 | 声明与源码复读；Popup/混合DPI NotRun |
| 创建序号除保留数量会出现4/3 | 显示真实创建序号，缓存数量单独说明 | 源码复读；运行NotRun |
| 放大工具条在220ms终点后缩回48 | 保存实际measured高度，移动与重锚复用工具条尺寸 | 两条P1控制流已独审复读；执行NotRun |
| 首批正文用旧Plan测量漏掉新控制行 | MeasureBody/MeasureMarkdown使用传入的新plan计算额外高度 | 两条P1控制流已独审复读；执行NotRun |

独审最终限定结论：指定静态复审范围未发现剩余阻断；Native ABI声明、引用平衡、菜单倍率/矢量模板未见明显静态错误。**不是整片构建、执行、像素、实时通知或正式产品签收。** 本片新增6个倍率几何参数用例、1个工具条生命周期用例、1个原文复制代次用例，仅有源码，全部NotRun。

## 尚待验收

本片构建/测试动作对当前dev源码在执行前因内容余额0阻止，未提交执行；不是新的自动审批拒绝。SDK402仍未安装，禁止下载。真实Windows同viewport前后图、200/225%字体实际测量、极小工作区、菜单/焦点、混合DPI/负坐标/命中、剪贴板及性能仍无证据；没有用云预览或示意图替代。原安装来源、签名、生产pins、图片复用资格及23项Core继续分别未验。不部署、不合产品main、不Release。

当前只读源码快照 `followup-source-inputs.json` 共563个输入；`evidence-followup.json`明确区分历史340与本片NotRun。全文77边界/24选点/26图册/18原生验收表保留，文本倍率相关行更新为“源码候选、未构建/未运行”。
