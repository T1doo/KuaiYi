# 安装源资格的封闭扩展清单合同修复

父级独审发现确定P2：原RequireExtension只核三项permissions和两类host权限，未拒optional_permissions/content_scripts；修改manifest并重新固定合法source SHA后仍能获得物化资格。它不是突破SHA，而是源资格合同不完整。保留原586签收日志及Library原件，不回填早轮结论。

本片只改该源资格门与定向测试，不改扩展本体/manifest权限、不接真实安装或注册入口。设计独立只读限定ACK：root仅允许既有十字段，嵌套对象也要求明确字段集合，未知/缺失/重复均拒绝；后台固定service-worker.js/module，options与icons固定既有本地资源，CSP固定既有精确字符串；action仅受界default_title，name/description为受界展示文本，version固定当前0.0.1。三项permissions保持精确集合，不因重新固定源SHA而允许扩大权限。optional_permissions与content_scripts即使空数组也拒绝。

合成测试沿MaterializedPayloadSource实际构建source，改manifest后重新Catalog并断言摘要变化，使用新合法SHA调用真实Apply；拒绝必须在创建安装根目录前发生。正向保留当前本地结构、允许展示文本和权限排序变化，经Apply/ReadActive/RequireMaterialized闭环。底层仍为真实普通managed产物与明确structural runtime/native/第三方fixture，非可运行Windows包。

原件artifacts/2026-10-22-extension-payload-contract保存完整命令、日志/TRX、失败XML、源码/输入/DLL摘要；各轮不相加，不重跑无关全套。真实Windows/Chrome/Edge/medium、身份/注册/安装/250600仍未验；三项nativeDLL和完整自包含runtime输入缺口保持。没有新增依赖、许可、凭据、安全权限、费用或真实模型调用，不操作用户电脑或重探403 runner，main不改。

## 确定性红绿

r01在原fe736d0 HEAD顺序六个普通构建命令成功，884输入稳定/锁不变，为实际source fixture准备相同revision的七角色managed输入。r02定向16结果4P12F，12新增结构均No exception；三项旧权限拒绝控制与合法展示/排序正例通过。r03同16测试16P0F0skip，两轮884输入稳定，输入差异仅InstallationPayload.cs，未改测试源码/断言。独立只读源码/红测限定ACK，无新确定遗漏；冻结后仅四相关类（新合同/物化/注册/角色）签收，不借记旧586全套。
