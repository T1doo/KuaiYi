# 受限 Markdown 显式 HTTP/HTTPS 链接

父提交 `8fa5cb38cdd43467302c7ad8a27d28aad2fb7e91`，正常开发分支 `dev/overlay-explicit-links`。已核初始干净工作树、单worktree、HEAD/默认网络远端一致；root唯一写入者、inline_review只读，未知锁和进程不动。

## 交付与边界

SPEC11.5原先全部`[]`字面回退的缺口现接平面`[label](http[s]://...)`。Core产精确Source、显示Text和不可变目标；段落/标题/顶层列表/基础表格共享原模型、实际FlowDocument/Run/Hyperlink。裸URL不自动链接、代码内标记惰性；图片、嵌套/引用/标题式链接、未闭合、复杂或非HTTP(S)语法使整个原块/表格保文，无局部激活或丢字。复制仍为原始OverlayResults.Text。

目标最多2048 UTF-16单元、平面label最多512；原120000格式化字符/4096 inline及块/表格预算保持，超限完整回退。只接受明确HTTP/HTTPS；拒绝userinfo（含空@）、控制/格式字符、反斜杠、原始空白、坏百分号及危险分隔符。Uri规范化后显示小写ASCII IDN域名/非默认端口，IPv6带括号、数字/十六进制IPv4显示点分地址；解析无DNS或I/O。保守子集可将合法复杂URL字面回退，包括原始括号及补充平面字符；百分编码括号可接受。

Hyperlink不设置NavigateUri，不使用XAML/HTML加载器或WebView。正文才挂Click，测量无动作；ToolTip显示域名和规范URL。每次显式点击暂停跟随，再核所选版本、元素存活、展开/可见、非图片/peek、无Suspension/Hidden/退出。默认取消的本机确认框先显示目标域名和完整地址；确认后重验同一目标、version/element/reader/result/epoch，才以ProcessStartInfo.FileName=规范URL、UseShellExecute=true请求系统浏览器，Arguments为空，无解释器、自动打开/加载或重试。

稳定块和不变单元格保留原Hyperlink；尾段、单元格替换、换版、回退和清空解绑Click并清目标/ToolTip/文字/闭包。Clear即使来源预览清理失败，也在finally释放译文链接，原待擦除图片owner仍供重试。确认中仅后续尾段增长可打开当前稳定链接；当前链接单元格替换则拒绝。暂停后恢复仍因epoch变化拒绝旧确认；Off保留结果可接受一次新的显式动作，不启用采集或请求。图片提示改为“本地预览尚未翻译、未发送”，On/Off均准确，无P6扩展。

## 实际回执与独审

最终Provider **1014/1014**，0失败/跳过；父923测试名多重集合完整保留，新增91：55目标/解析、34生产链路、2图片文案。链接实际FlowView/Region/Actions/OverlayLinkFlow和原有Flush/Measure/Source/Clear，只替本机确认/launcher；原生控件是明确Linux数据调用端口，不模拟WPF路由或排版。真实Core、RuntimeGate、三版本缓存和Coordinator执行，新增用例仅合成地址/无socket connector，未打开真实外部网址。完整套件仍含原MockProvider本地HTTP测试。

覆盖所有分隔符两分片/逐UTF-16、CRLF/转义/代码惰性/整表回退/Source/预算；测量无动作、确认拒绝/启动故障、reader/result/选版/替换/Stop/取消/Capture/Settings/隐藏/锁屏/退出/清空、稳定尾段和单元格、Coordinator四次生成后三版选择/Stop/Clear/迟到隔离。旧两条HTTP(S)字面断言升级为明确正向Link断言，原测试名/输入保留；表格IsDocumentEnabled为true以接Click，无NavigateUri，其余只读/禁止Drop/滚动断言保持。

- r01定向186/188：两条新测试在链接已被正确拦截后，错误要求Capture/Settings令已有Append失效。原RuntimeGate契约是暂停采集、增epoch而保已有请求身份；修新测试额外断言，保无launch断言，补瞬时暂停恢复。不改Gate或原安全门。首调用会话退出码未收集，TRX明确Failed，log/TRX完整保留。
- r02定向219/219；r03完整1007/1007；r04 App0W/E。
- 独审无阻断；补空userinfo一律拒绝、混淆IPv4规范显示及确认中稳定尾段/替换单元格用例；r05最终1014/1014，r06最终App Release交叉构建 **0警告/0错误**。

10份新原始log/TRX唯一精确-text保全，父81份原件逐字节/hash不变，无覆盖。源码manifest、计数/失败与保全见[固定证据](../test-evidence/2026-10-15-overlay-explicit-links.json)。所有dotnet均提前export且命令前显式`DOTNET_GENERATE_ASPNET_CERTIFICATE=false`。

真实WPF鼠标/键盘路由、确认框焦点/可访问性、浏览器启动、布局/测量/选择/阅读锚点/像素释放、COM/broker/所有权/取消时限及B06/B12/B07、完整P2/P6/11.5仍 **NotRun**。本批没有真实外链导航、外围工具、Windows CI或CMake/Windows实验。250/600ms和来源/密码/完整性/取消/清理门保持、生产离线复用Off；证书存储/信任与旧覆盖前字节Unknown不读改/重造。Actions不查询不绕Forbidden、远端run数Unknown；无main/强推/部署/依赖费用凭据权限协议或真实模型。
