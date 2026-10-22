# 受界渐进 JPEG 解码

从 `ccc5d9c93837a3ccaec0b148b069914f478c4e84` 在原指定环境、唯一writer接续。SPEC §7.1 的真实缺口是 JPEG 仅单完整interleaved baseline，现有打开/文件投放/浏览器原图遇SOF2必拒。独立设计和实现只读审查限定ACK后补同一Worker管线，不改主入口、协议、来源身份、预览/On/逐次发送门、生产复用Off或WebP odd拒绝。

## 最小产品改动

`ImageRasterDecoder` 在native前分派；独立 `ProgressiveJpegScan` 支持8bit Huffman SOF2，1/3分量、H/V各1或2、sum(H*V)≤10，灰度仍H=V=1。旧 `BaselineJpegScan` 字节不变。前缀在1024段内找到SOF2才进入新校验器；超界交原baseline验证器，保留原baseline长头部兼容性，SOF2仍由旧拒绝门阻止native。没有通过catch吞错或按native Success放行。

新校验器验证DQT/DHT/SOF2/SOS、谱段/分量、初扫与逐bit-plane refinement、DC实际带符号差值及预测、AC非零mask、ZRL/EOBRUN、字节stuffing、全1 padding、RST顺序与精确EOI，无尾字节。单分量使用实际块数，多分量DC使用MCU补齐布局。EOBRUN不能越scan/RST，所有分量64系数最终precision均到Al0，缺末次扫描不能以部分图成功。AC initial size+Al≤10，refine新增非零size=1；DC checked predictor/long shift限定[-1024,1023]，防止native short转换后与mask语义分歧。DHT允许合法重定义，首scan后DQT明确拒绝。

上限为64扫描、1024个带长度segment、16MiB mask、累计256百万系数位置工作量；RST按MCU数单独验证，不作metadata段计数。所有预算在分配/循环前checked计算，每MCU可取消，mask在finally清零。mask只保存AC非零位，不重建像素；实际像素仍由原Skia解码。默认null的内部观察接缝仅供确定性清零/取消测试，无外部入口。现24MP/尺寸/分量约束的mask粗界≤9,196,704B，16MiB守卫更宽；未伪称已触发16MiB临界拒绝。

原20MiB、24MP、16384边长、2560准备长边/4MiB PNG、原SHA/job、EXIF方向/无桌面坐标、Worker实际退出及取消清理不变。没有新增依赖、许可接受、权限、凭据、CI或模型调用；ImageMagick仅已有测试工具，不是生产依赖。

## 固定源码依据

已保全固定Skia的[DEPS](https://raw.githubusercontent.com/mono/skia/45afab4f1f0921f3feb97f58cf89b136fffa85e6/DEPS)与[SkJpegCodec](https://raw.githubusercontent.com/mono/skia/45afab4f1f0921f3feb97f58cf89b136fffa85e6/src/codec/SkJpegCodec.cpp)正文/hash。该progressive路径输出最后完成的scan，所以不能把GetPixels Success当完整性证明。DEPS固定libjpeg-turbo `9217719d3a58633923b096af4c1d50d304768a64`；其[progressive实现](https://raw.githubusercontent.com/libjpeg-turbo/libjpeg-turbo/9217719d3a58633923b096af4c1d50d304768a64/src/jdphuff.c)对部分progression错误仅warning，且[JCOEF定义](https://raw.githubusercontent.com/libjpeg-turbo/libjpeg-turbo/9217719d3a58633923b096af4c1d50d304768a64/src/jmorecfg.h)为short。本片自有结构预检不能依赖这些宽容行为。源正文摘要不是发布二进制可复现或完整安全审计证明；仍使用原获准四个4.153.1包/锁，不还原新依赖。

## 已实际运行

- r02漏为既有ImageDecodeFixture链接新文件，CS0103编译失败，无测试执行/TRX；只补该Compile链接，不算红例。
- r03保全旧Worker真实红门4执行/0P4F，四个真实非零渐进fixture均UnsupportedImage；不是把SOF0标志改C2。旧binary及摘要保全。
- r04新Worker对应4图和原伪header拒绝5P0F。旧伪渐进仅SOF0改C2却保留Ss0/Se63，现准确仍拒InvalidImage；未删旧拒绝测试。
- r05相关37P0F0skip；其中36为新增。独立Sparse Huffman流和DCT公式验证正/负新系数、existing correction、initial/refine ZRL/EOB；18坏流direct native input allocation=0并经实际Worker同拒，真实非零mask取消/observer异常后清零。初版搬表只搬DC，独审指出区分力不足，下一轮改为按原顺序移动全部DHT、明确含0x20 EOBRUN AC表且旧Baseline确拒。
- r06相关50P0F0skip。含修正搬表、FF stuffing、真实63/64扫描、65扫描拒、1023/1024段及溢出、24MP depth9工作237.256M接受/depth10工作260.944M拒、3,008,000B mask核账、维度先拒/precancel；LocalFile/Browser实际准备EXIF6、无桌面锚点，原child held-exit取消/清零/同original join。
- r07相关3P0F0skip。独审发现前缀超限会误拒原baseline长头部，修正仅回原验证器；1025 COM的真实baseline direct/Worker仍成功，同前缀SOF2 direct native0/Worker仍Unsupported；前后段数溢出门保留。

四JPEG由已有ImageMagick7.1.1-43对明确合成PGM/PPM生成（gray/444/奇数420/420+restart），命令、源码字节公式、尺寸、hash保全。四真fixture的同Skia对照只证明完整输出一致；独立DCT是小型系数数值验证，都不等于照片画质评审。

r08私有执行器对Git带引号的中文路径处理错误，SDK执行前FileNotFound；无测试/构建执行，无TRX。改用`git ls-files -z`读取原路径，原失败保全。

r09固定产品/测试树 `30b010f181a22b64b5e3288963c1953dff1f0268`，仅四类Codec（RasterDecodeTests、RasterPreparationTests、FileImageOwnerTests、DecodedFileOcrTests）175P/0F/0skip/exit0；App与Worker正常Release交叉构建各exit0/0W/0E。124个原选择用例中仅原伪渐进测试改准确名称及InvalidImage期望，其余保留，新增51例。各轮不相加，不重复完整Core/Provider/Codec，不借记旧全套。

完整命令、TRX及零失败XML、日志、858输入前/各阶段后/最终摘要一致且与固定Git匹配，4个目标AssemblyInformationalVersion均含完整固定SHA；339 DLL摘要为产物库存而非执行数，当前字节复核一致。`BaselineJpegScan`与ccc5d9c字节相同。原始TRX/命令、全部858输入、339 DLL与4份AssemblyInfo及三报告已获独立限定ACK，无确定新P1/P2；ZIP独立CRC/逐成员摘要核验通过，Library确认 `libfile_643983e0b1f4819199b88f4dd44b9cdb/v0`，590,992B/99成员，SHA256 `c6eeef6ab674dd30137fdf7a008381e3d2d9a06ff8d959e86aa95db378984a1e`；上传后本地xattr与ZIP不变复核通过。包内报告为封包前快照；旧Worker本体仍本地保全，包内仅其摘要/来源，不含468MiB旧binary。

## 未验与保留边界

这不是全部合法JPEG支持：算术/无损/12bit/4分量/更高采样及超scan/segment/work、过大系数或不完成到Al0等仍明确拒绝；原其他格式边界保持。没有真实Windows/Chrome/Edge/WPF/OLE/medium/来源身份、本地OCR效果/照片画质或250/600ms与600MiB验收；P6/P7整体仍InProgress。旧失败原件不改绿，PackageHold、main/强推/发布部署及用户电脑禁区保持。
