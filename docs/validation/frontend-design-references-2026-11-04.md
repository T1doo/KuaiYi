# 快译浮窗设计参考与取舍

> [!note] 状态
> 设计研究与实现约束，非桌面验收报告。当前用户要求优先于原 DOCX 中克制、白色的外观方向。原 DOCX 保持不变。

## 用户确定的方向

彩色磨砂玻璃、精致中文排版、可选择静态或动态背景、自由拖动、有机或云朵轮廓。译文逐步输出时，容器随内容生长。保留 48 DIP 锚点、既有展开／收起／显现时序，以及取词预算和全部安全门控。

## Figma Community 检索

2026-11-04 在公开 Community 页面实际检索了 glassmorphism、dynamic island、mesh gradient、blobs、desktop widget 五组关键词，读取列表名称与公开页面信息。插件本身没有全站 Community 搜索能力，因此使用公开浏览器。浏览器截图调用未成功返回；以下不能当作已经播放或检查了内部原型的证明。

| 参考 | 研究用途 | 检查范围 |
| --- | --- | --- |
| [Glassmorphism All in one](https://www.figma.com/community/file/1001027739810154285/glassmorphism-all-in-one) | 玻璃组件与边缘层次候选 | Community 搜索列表 |
| [Glassy — 12 Trendy Glassmorphism Cards](https://www.figma.com/community/file/964461263114726217/glassy-12-trendy-glassmorphism-cards) | 材质与配色候选 | Community 搜索列表 |
| [Fractal Glassmorphism](https://www.figma.com/community/file/1266701956665670106/fractal-glassmorphism-dark-mode-light-mode) | 明暗玻璃表面候选 | Community 搜索列表 |
| [Light Mesh Holographic Gradients](https://www.figma.com/community/file/1095832504407038817/light-mesh-holographic-gradients-brix-templates) | 多色柔和过渡候选 | 公开详情标题与预览资源信息 |
| [Dark Mode Mesh Gradient Blobs](https://www.figma.com/community/file/1195043324904003773/dark-mode-mesh-gradient-blobs-brix-templates) | 深色背景光晕候选 | Community 搜索列表 |
| [Attablob](https://www.figma.com/community/file/1199696340012094923/attablob-create-animated-blobs) | 有机形状相连、变化的研究线索 | 公开详情；作者描述为原生组件动画，未播放内部原型 |
| [Liquid button experiment](https://www.figma.com/community/file/1203628046750997056/liquid-button-experiment) | 液态按钮交互候选 | 从 Attablob 作者回复找到，核实公开详情标题 |
| [Animated Liquid Blobs](https://www.figma.com/community/file/1260309937754496559/animated-liquid-blobs) | 自由轮廓动效候选 | Community 搜索列表 |
| [Figma Dynamic Island Prototype](https://www.figma.com/community/file/1151731637711831885/figma-dynamic-island-prototype) | 小形态到展开形态的连续性候选 | Community 搜索列表；作者公开介绍亦可检索 |
| [Apple Dynamic Island Animated — Auto Layout + Components](https://www.figma.com/community/file/1149679181793588525/apple-dynamic-island-animated-auto-layout-components) | 内容与容器联动候选 | Community 搜索列表 |
| [Asana desktop widget concept](https://www.figma.com/community/file/1015973888935413833/ui-kitconceptasana-desktop-widget-macos) | 桌面工具的信息密度候选 | Community 搜索列表 |
| [Simple Chat Widget for Desktop](https://www.figma.com/community/file/1292857988526681686/simple-chat-widget-for-desktop) | 小窗口阅读布局候选 | Community 搜索列表 |

另阅读 [Figma 官方 shader 指南](https://help.figma.com/hc/en-us/articles/41409034424215-Built-by-the-Figma-team-shaders-and-plugins)，研究 Gooey merge、Mesh gradient 等设计概念。它们是视觉参考，不代表 WPF 已实现相同渲染效果。未导入、复制或安装社区资源。

## ui-ux-pro-max 的适配

用户明确指定 `C:/Users/Lenovo/.codex/skills/ui-ux-pro-max/SKILL.md`。已读取 skill、pro-rules 和 quick-reference 的相关规则。

- `desktop utility glassmorphism vibrant --design-system` 返回网页 Hero/Features/CTA 和 Swiss Minimalism，平台与用户方向不符，未采用或持久化为设计系统。
- `glassmorphism --domain style` 命中 Glassmorphism，采用层次、柔和背景、细边缘高光的视觉原则；CSS 透明度和 blur 数值不直接套用到桌面 DWM。
- `animation rendering typography --stack wpf` 命中 ControlTemplate，采用原生控件模板统一按钮视觉，保留语义和键盘行为。
- 动效搜索返回部分 VisionOS 条目，不采用眼动追踪等无关建议。连续装饰动画的通用反对建议不覆盖用户明确要求；提供静态选择和减少动画支持。
- 接受普通文字 4.5:1 对比度目标、可见键盘焦点、一致图标粗细、按压反馈、拖动阈值、可中断动效、系统文字缩放。实际桌面测试后才标记通过。

## 接下来落实的设计语言

1. **轮廓**：圆形锚点作为生长起点；玻璃、云朵、流体、泡泡轮廓须保持内容安全区。WPF 裁剪与原生点击区域一致，避免透明区域挡住桌面。
2. **材质**：磨砂底层、分布式色晕、细边缘反光；正文背后有足够稳定的明度。主题配色联动按钮和边缘，不仅替换背景。
3. **排版**：内置中文字体与系统字体可选；字号、行距、字重、段落留白一起校准，不把英文展示字体当成中文方案。
4. **动效**：真实内容推动窗口增长；文字不随外壳缩放。维持既有时序，快速取消和反向收起保持位置连续。背景光晕缓慢移动，减少动画时静止。
5. **交互**：按钮 hover/press/focus/disabled 全状态统一；拖动区域与文字选择、按钮点击分开，Esc 不吞输入法正在处理的取消语义。

## 本轮工程状态

配色、静态／流动背景、字体选择已接入设置草稿的加载与保存路径，并加入本地外观预览。当前构建日志 `artifacts/frontend-aurora-preferences-build.log`：Release 编译 0 警告、0 错误。这仅证明编译；最新字体解析、设置往返、实际玻璃、自由轮廓、中文输入法、拖动与多 DPI 仍需分别验证。没有以旧版通过记录替代新版测试。
