# P2 浮窗 Windows 待执行清单

本清单尚未执行。Linux核心/协议/构建和实际Windows拖动/渲染明确分开；所有Windows结论仍NotRun。本包是框架依赖win-x64审查文件，需要Windows11及.NET10 Desktop Runtime，没有安装器/身份注册/原生DLL。

`app/KuaiYi.App.exe` 是正常无测试数据的正式入口。公开流工具位于独立 `public-probe` 目录；`public-probe/KuaiYi.GrowthHarness.exe --actual-overlay <CSV路径> 30` 使用同一个生产ShellWindow，来源只有工具内公开文字、临时合成AuthNone配置与真实profile-bound身份，没有模型或外部内容。速率可为5、30、100或`burst`（每批2000字符）；F5显式重开公开流。旧P0 Harness保持原命令，不作为生产浮窗证据。

1. 首批正文前仅进度环；400ms后最多220DIP连接条。首批内容按最终宽度排版，220ms无回弹展开；每50ms合并测量，后续高度平滑增加并冻结方向。收起180ms、前80ms淡出，动画中反向不得跳边或透明度闪变。无数据/几何收敛停止帧回调；等待环仍需真实测CPU。
2. 四角、四边、中央、负原点、窄工作区逐帧检查计划/实际HWND与实际WPF按钮屏幕矩形。CSV包含nativeMatches、actualButtonMatchesAnchor、实际按钮四边、实际可用区检查和前台是否变化；原始记录只存坐标/时间/布尔，不含原文。查看录像及帧记录后才能判定B组，不把工具summary的NotEvaluated当Pass。
3. 展开、收起、回看50次，包含中途反向；按钮锚点不变。Off回看缓存保持红色、没有采集/请求；手动收起后本请求后续数据只缓存，不撑开。停用取消，部分内容可回看；锁屏/退出清内存并取消，解锁只有红钮。
4. 同屏展开态拖动保持可见尺寸，文字继续缓存；整体卡片夹紧工作区。跨125/150/200%DPI时保留同一次捕获手势、原始按下抓取比例、Moved闩锁与资格，重新基准屏幕坐标；松手必须Released并保存，不能变成Click切On。拖到较小屏允许立即缩小以确保A⊆C⊆W，松手后重规划/测量。包含“拖动时首批正文到达”以及动画未完就拖动。
5. 监视器移除、任务栏/工作区变化、系统取消鼠标捕获、锁屏或隐藏：废弃旧过渡并安全收束；消失屏可夹紧到可用屏，取消不能误触启用，保存最后合法位置。普通主题事件在拓扑未变时不重开卡片。真实Windows消息顺序和WPF DPI transform处理必须记录，不用合成事件顺序证明实机。
6. Hover350ms工具条、离开整个按钮/条250ms收回；跨越按钮到设置不闪退，键盘焦点留在条中不收回。展开控制栏保留主钮、收起和停止；很窄屏通过主钮右键/键盘菜单访问其他操作。正文选择/复制显式激活，其他自动生长不得抢前台。
7. 到高度上限内部滚动；距底24DIP内才跟随，向上阅读保持滚动位置并显示“有新内容”。向上生长不反转文字。当前源码已接受限Markdown原生段落/标题/列表/行内/代码/表格及局部滚动；历史审查包不会自动更新。表格/长代码/200%系统文字/高对比度/减少动画的实际排版与无闪烁仍待验，新增跟随协议测试不能替代实机。
8. `pwsh scripts/ci-offline.ps1 -Configuration Release` 可运行实际锁定构建、便携HTTP/协议测试和Node测试，输出解析原TRX/JUnit的计数。它不是Windows桌面测试；本清单B组无对应Windows实机通过证据，既有workflow及阶段运行按各自记录。不得因为CI脚本零退出便声称P0/E06、B组、60fps或安装卸载通过。

检查正常生产app目录无PublicText/MockProvider/GrowthHarness角色；测试流只在工具程序集。本包包含公开探针以便复核，并不称为开发安装包。

## 2026-10-15 当前源码的原生 Markdown 验收准备（未执行）

指定环境是 Linux x86_64，无 DISPLAY/WAYLAND 和 WindowsDesktop 运行时；没有尝试 Wine、远端 Actions、Windows/CMake 或桌面探针。下列步骤只为未来已授权的 Windows 交互会话准备，本次全部 **NotRun**，不是操作授权或 Windows 通过回执。旧入口/CI输入不变，新 `--actual-markdown` 只选择既有 Production ShellWindow 工具内固定合成语料；禁捕获/Translation/Drop/Hotkeys 后端、无模型/凭据，不读取外部文件。框架依赖输出不是安装包，不注册身份、不签名或信任证书。

如使用 Windows 11 与已安装的 .NET 10 Desktop Runtime，按当前源码构建，所有 dotnet 前保留 false 变量；实际运行需单独记录完整提交/工具摘要、系统和 DPI。以下命令本次未在 Windows 执行：

```powershell
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
dotnet restore tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj --locked-mode -p:EnableWindowsTargeting=true
dotnet build tools/KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
tools/KuaiYi.GrowthHarness/bin/Release/net10.0-windows/win-x64/KuaiYi.GrowthHarness.exe --actual-markdown artifacts/native-markdown/run.csv 1000
```

固定输入 V1 的源码为 `tools/KuaiYi.GrowthHarness/MarkdownReviewText.cs`；[逐字节文本](test-evidence/2026-10-15-native-markdown-v1.txt)仅含合成公开数据，32575 UTF-16 单元、35747 UTF-8 字节，SHA256 `4b2a6cd375c4fa39d56d3a7a685840f81545bd4085d2124c66ee6272679b863e`。两项真实 Core 测试核固定摘要、完整 Source、最终128行代码/12列65行表和137字符分片；它们不是 WPF 排版。1000字符/秒约33秒；F5重新开始同一固定流，`burst`用于突发。不要把测试文本换成用户原文或真实配置。

分别记录以下场景，缺任何实测数据均保持 NotRun：

| 场景 | 实际操作与应观察结果 |
|---|---|
| 有限视口/结构（B12） | 代码区横滚，表格横滚/纵滚，普通1200无空格字符和中文标点换行；表头/左右中对齐/Run/Span保留；实际 HWND、正文页宽、滚动条不撑出工作区 |
| 自动跟随（§4.6/B06） | 在末尾代码或表格生成期间距局部底部24DIP与25DIP分别追加；仅前者且外层仍follow时跟随；首次挂载按follow展示尾部；主题重载不重新强制跳底 |
| 阅读暂停与明确恢复 | 表内滚轮/拖选/键盘上读，即使外层到底也保持锚点；“有新内容”只使当前末尾区域和外层到底，水平偏移和原选区不变；继续追加可跟随 |
| 尾部结构变换 | 代码闭合转普通段落、部分pipe行变表格末行；不再是末块时仅恢复原选区/偏移，不能跳旧区底部；变为末块按incoming结构判断24DIP |
| 输入交错/清理 | 新输入抢在Loaded恢复前、反向排队、F5新结果、锁屏/隐藏/退出；旧回调不能改新阅读状态或保留旧内容；正常清理完成再结束记录 |
| 选择复制 | 单格/多格、代码、跨嵌套区域分别试选及复制，只用固定语料；完整原文复制与局部选择分开核，不假定独立编辑控件支持整文统一选择 |
| 尺寸/可访问性 | 100/150/200%DPI、极小可用区、200%文字、高对比度、减少动画；实际 DesiredSize/阅读位置/按钮边界及B06/B07需对应原始实机证据 |

工具 CSV 仍只记录时间、坐标、布尔，无原文/截图。summary 的 `DesktopAcceptance=NotEvaluated`、交互程序 Exit0 或几何检查为 true 均不代表上述场景通过。自动无输入场景的 ForegroundUnchanged/AnchorStable 与主动点击/拖动场景分开评估：用户交互可合理改变前台或锚点。原始 CSV/summary、人工操作记录和仅含合成窗口的必要图像一起核验后才能更新状态；本次没有创建任何 Windows截图或测量值。详见[本轮生产改动与证据边界](validation/overlay-tail-follow.md)。

## 所选版本原文与 Stop 的补充场景（当前环境 NotRun）

2026-10-15 再核指定环境：Linux、仅 NETCore/AspNetCore 10.0.12、无 WindowsDesktop、无 DISPLAY/WAYLAND。交叉构建及数据端口不能执行下列验收；没有新增 Windows CI、兼容层或远程探针。最小解阻是已有授权的 Windows 11 交互式桌面与已安装的 .NET 10 Desktop Runtime，按上面的现有固定合成入口运行当前提交，记录完整 SHA/运行时/DPI 和原始 CSV/人工操作结果。此处不授权安装、安全设置变化或真实模型调用。

1. B06/B12：在长代码、长表格中上读、选择并横滚，生成中点击 Stop；实际原文/译文、局部选区、双偏移、外层阅读位置和“已停止”标签一致，迟到内容不接纳。Stop 后 Off 回看、重开已保留版本不发请求，不误用最新采集原文。
2. 文本原文折叠：所示版本有快照时，在长译文顶部通过工具栏/菜单“查看原文”，检查真实 Expander 与 BringIntoView；默认折叠、纯文本保留空白/换行、只读可选，代码/链接字符串均惰性。展开高度受限，整体仍在工作区；字号/DPI/高对比度、极小窗口、嵌套滚轮和选择需真实验证。显式定位后立刻上读、换版、失败回退、收起、隐藏、锁屏和退出，旧定位不得拉回或再显旧原文。
3. B07：收起/展开连续50次，包含动画中反向；另在原文已展开、代码/表格阅读暂停、Stop和历史版重开状态重复。核按钮锚点、无负尺寸/闪烁、最终状态服从最后操作；同条生成的生长方向冻结规则保持。没有原始实机记录前不得标Pass。

固定 Markdown 工具禁用真实 Translation/采集后端，所以它本身没有 SelectedSnapshot 原文，不能冒充原文/历史版本场景。相关真实版本场景需在既有获准本地合成 provider 审查入口提供固定源快照；当前工具不足处明确记录 NotRun，不为凑通过调用模型、抓取真实文本或增加外围验证工具。图片预览、一键清空生产入口以及完整P2仍另有未完成项。

## 清除当前内容补充（2026-10-15，实机NotRun）

当前源码新增主菜单/浮窗更多/设置隐私页的同一“清除当前内容”命令；前述历史的一键清空缺口由本批补齐，旧实机状态不回填。未来已获准Windows会话只用既有合成输入：分别在生成/确认/代码或表格上读/原文展开/三版本/On与Off/设置打开时清除，确认内容与旧版本不可再回看、设置和凭据保留；清理中再起新任务/再清一次/退出，旧提示和Restore不得回写。另在Drop退出等待Run/Control/owner时让截图finally触发迟到Refresh，确认停止锁存不建立新接收器、不重复终结同peer；失败保原owner待显式重试。现有本地截图选择/预览及Drop清理还需各自真实窗口、像素、所有权回执；未确认时应明确提示，不能依据数据端口或交叉构建宣称退出/归零通过。当前环境仍无WindowsDesktop，全部NotRun；图片原文预览留下一轮，不新增探针/CI/真实模型。
