# 桌面界面精修：真实接线与验证边界

独立视觉线因snapshot_materialization_failed/gateway_unavailable结束，未建分支、未写文件或提交。原环境唯一写入者在完成分类边界P2独立修复后，恢复隔离暂存并直接精修现有C#窗口。没有等待不存在的视觉commit，也没有仅加入孤立XAML资源。

设置窗口接入184 DIP导航、品牌区、页标题/说明、可滚动表单卡片和底部状态/保存区；同一七页与原控件实例继续使用。浅/深配色、枚举显示标签、间距和窗口本地样式统一；主题选择仅预览当前设置窗外观，保存/取消仍走原草稿事务。输入/密码/下拉框保留系统模板；OCR/发送确认模态窗未加全局隐式样式。本片不改安装器。

浮窗沿用原48 DIP主按钮、28 DIP工具、锚点/布局/计时与HWND裁切。工具保留原Content/Tag/Click及自动化名称，用模板显示矢量图标并加tooltip；原可见来源、首帧、状态标签未改。主按钮仍按SPEC红/绿启停，高对比取系统色。独审发现动态高对比主glyph未刷新P2，已在纯外观RefreshPresentationTheme中按当前状态刷新底色/glyph，未调用有业务副作用的Refresh。静态系统事件在实际Closed时解绑，排队回调再检查closed。

只读独审限定ACK：未发现新确定P1/P2。与b473317的源码逐字比较确认：Settings构造LoadGeneral后的全部事件尾段（含只读第5页例外、Mask、选择回滚、保存关闭等待）、从LoadGeneral起全部状态方法、浮窗PresentationMenuClosed起几何/动效/菜单/清理方法均不变；摘要见原件source-equivalence.json。Shell仅把原主按钮外观刷新迁入纯外观方法，自动化/tooltip/托盘逻辑保留。未改来源/密钥/取消/清理/隔离/发送门、250/600ms和图片格式门。

## 实际验证

原件 `artifacts/2026-10-22-desktop-polish/`，各轮保留命令/日志/TRX/输入/产物摘要和候选源码，不相加：

- r01初版App交叉构建0W/E（不含后续枚举/HC调整）；r02实际Palette2P。均早期候选，不代签最终代码。
- r03最终UI候选正常App Release交叉构建0W/0E；872输入前后无漂移。
- r04同最终候选相关259P/0F/0skip，872输入无漂移：新增2例直接链接生产Palette，核正文/次要文字/选中/主动作对比度≥4.5及输入边框≥3；其余257为既有设置秘密命令、显示偏好和浮窗owner/阅读/停止/清除/滚动逻辑回归。这些端口测试不执行新WPF模板、布局或真实焦点。

未为本片重跑无变化的完整Core/Provider/Codec；不累计旧通过。候选执行时Git HEAD为b473317且UI dirty，最终源码提交后逐输入核Git匹配；不把dirty构建的AssemblyInformationalVersion写成新提交的版本。

## 像素与平台边界

原件含自包含离线 `desktop-design.html`，配色与工具path从生产源码生成，明确标注“合成示例 / 非Windows WPF截图 / 不保存不联网”；只是设计对照，不是产品入口或WPF运行证明。尝试现有Chromium离线sandboxed渲染：失败SIGABRT，SUID sandbox helper配置不完整（报需root-owned/mode4755），错误原件保留。未改权限、身份或关闭sandbox，未生成或声称有截图；不是审批拒绝。

真实Windows像素、深色系统ComboBox/Popup、键盘焦点、动态高对比、200%文字/多屏DPI及布局均NotRun，须在已有合法普通权限Windows交互环境验证。Windows/OLE/身份/medium warm/Explorer/Chrome/Edge/实际OCR质量及250/600ms仍未验；没有使用用户个人电脑、403 runner重探、新CI/权限/SDK许可/费用/真实模型调用。阶段P6/P7/P8不因本片全绿变为完整通过。

最终UI产品/测试源码提交 `b03541c468180e84de33731dafba7b7fd78c50e4`，872输入逐Git一致。执行时dirty候选HEAD/产物版本仍 `b473317311646ec7b1d36c88e8659ad18641033b`；没有仅为版本号重复无变化测试。相关259条属13真类，TRX以testId关联TestMethod.className计数；DisplayPreferencesTests76条。第一次summary误按参数化testName的小数点拆分出伪类名，独审发现后已修正，原错误summary保留；总259=新2+旧257及产品结果不受影响。

原件Library确认 `libfile_7811cbea5c5881919759c41e1c92b8ed/v1`；649806B/94成员，ZIP SHA256 `af3ce2ba145fb6137e3bb30c602af45fc66c30f9bf5c28039967add25b295b38`。v0保留为证据分类更正前版本；v1保存后返回xattr、LibraryID与摘要复核成功。该归档含可离线打开的设计对照HTML及截图失败原文，不含WPF或Chromium截图。后续仅三报告提交；先前自有隔离stash逐8路径核等于已提交产品后移除，无待恢复代码。

## 后续纠正文案 P2

父级独审确认全局“默认 Off · 按次确认”会误导用户以为每次发送均弹确认。实际SelectionFlow.AcceptSelection直接QueueTranslation；TextTranslationOffer.RequiresConfirmation仅OCR文字或超过软上限为true，普通合格选区可进入Started。最小更正仅SettingsChrome同一TextBlock.Text：**默认 Off（未启用）／启用后可自动发送选区／保存设置本身不发送内容**。没有为迎合文字改任何业务门。

[本片定向证据](../test-evidence/2026-10-22-settings-send-wording.json)记录基线84116e、逐字替换检查、其余345个产品源码文件与基线逐Git相同及完整App构建命令/日志摘要；[原始构建日志](../test-evidence/2026-10-22-settings-send-wording-app.log)为exit0/0W/0E。没有新增或执行测试用例、不借记旧259；本片只是文案/差异静态核验与候选交叉构建。独立只读限定ACK。原Library v1归档和HTML中的旧文案保留历史快照，自本片起不得视为当前产品说明；未覆盖旧证据。WPF真实像素、DPI、focus/highcontrast及原Windows边界仍NotRun。
