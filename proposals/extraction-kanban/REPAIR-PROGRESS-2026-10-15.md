# 四模块修复记录

依据：[源码问题报告与修复计划](SOURCE-AUDIT-2026-10-15.md)。状态和认领只在 [BOARD.md](BOARD.md#source-audit-repairs) 维护。本文件记录实际修改、验收证据和剩余边界；未执行的测试不写通过。

## 第二十七批：R3 / A 非作者复审（2026-10-18）

用户授权后，`review_guidance_r3`、`review_process_a`独立只读审查。R3模拟终态、HOME及EMR-020边界未发现问题；新增无界Bun启动器却能在导入阶段永久挂起，确认为本轮新增P2，已删除入口和WHAT正式声明，单列CUT-00-R2。Bun原生证据保留，不改写成正式监督通过。A未确认新增产品回退，独立Fable正反编译2/2、真实Node/Bun冷接管及无owner拒绝3/3；沙箱物理诊断失败单列，不记通过。

R3和A仅在声明范围标DONE，父卡、正式监督缺口及预算不随之关闭。详细证据见两包报告；本次修正未变更生产实现。继续C施工，R12-U1暂缓，未部署Host。

## 第二十六批：Process A 纯契约与真实消费者（2026-10-18）

输入主仓`57ba194b6`、库`10bbb32 / 0.6.4`。将ProcessRequest/PtyTypes四文件收敛为无ProjectReference的纯契约工程，物理实现移至独立runtime工程；删除无消费者的PtyOptions，将RunProcess/RecoverPty及显式PTY端口注入实际Git、工具、恢复与Fork调用者，删除隐藏默认工厂。无durable workspace的生产入口仍在Reserve/Spawn前拒绝，未放宽物理所有权。

012原反例转绿，真实Fable正反编译及实际Git/DelegationPtyCapability消费者2/2。原生Node128文件994项：897pass/0普通fail/25skip/72TODO；Bun9文件72pass/0fail/1skip/2TODO，实际Host1/1。完整003为60/60、025为124/124且正常退出；精确包隔离安装的Node/Bun真实run/wire和完整PTY124/124通过，2076文件前后摘要不变。包13,375,869bytes，SHA-256 `2e500c8e27a27aeb96c75c60e1c5f07429e2c8de4f3cdae01a293b58d36278ed`。构建、format、check及diff检查通过，详见[报告](PROCESS-CONTRACT-2026-10-18.md)。

原拆分缺口、本轮遗漏的Deadline工程引用、测试前提错误和撤回的句柄泄漏判断分列记录。正式监督包装器在修前包与A候选均会提前终止有界物理等待，新增CUT-00-R1；其无权威总计，不记为通过。既有GAP-092预算TODO仍失败，不因普通回归全绿而销项。A进入REVIEW，待非作者复审；正常提交推送master，未部署Host。继续C的库适配与主仓集成，R12-U1暂缓。

## 第二十五批：三小时施工，CUT-17-R3 定因与夹具修正（2026-10-18）

从干净`511a86d3e`开始，库`10bbb32 / 0.6.4`未变。定位后五项卡在首次chat.message，而非Pair重放或冷读：指导夹具未报告模拟provider确切终态，dispose留下Retiring；Bun又不响应后来修改HOME，加载外层推荐路由，累计16槽后等待。现在用注册Host终态事件结束自己模拟的exact执行，并检查owner/waiter消失，失败断言也收尾；统一测试路由定义，在启动Bun前创建自有HOME。另将同前提的EMR-020改为真正新OS配置验证，原断言保留。

同一个2e0f生产包、同一16槽配置，旧helper完整014为20pass/5timeout/1skip，新helper为25pass/0fail/1skip；新增单槽例旧helper先红，最终完整Bun27项/26pass/0fail/1skip。Node相关35文件280项/265pass/0fail/1skip/14TODO，runner三文件100/100、实际Host1/1、新OS冷读、Bun路由15/15及Node最后020补验7/7通过；正式runner收尾accepted=true。真实pack仍2076members/13,375,692bytes及2e0f摘要，修前后产品字节相同。原沙箱/输入缺失/额外测试前提失败均保留并分开归因，详见[报告](GUIDANCE-BUN-LIFETIME-2026-10-18.md)。

仅测试及文档修改，不改生产证据规则，不延长timeout、不拆014绕顺序、不删断言或加skip。R3进入REVIEW，待非作者复审；已请求本轮委派授权，作者自检不冒充独立评审。本包正常提交推送master，未部署Host。三小时窗口继续下一包；Process A/C/B及R12-U1尚未因此销项。

## 第二十四批：按最新授权重写修复计划（2026-10-18，仅计划）

输入主仓`70c7d52ac`、库`10bbb32 / 0.6.4`，两仓干净。用户明确允许大改、不迁就旧版本，已将[当前计划](REMAINING-REPAIRS-2026-10-16.md#current-plan)重写为唯一施工入口；旧计划正文由Git历史保留，已完成包只留证据索引，不再与新排期混放。同步修改看板和施工指南，取消默认旧API/事件/格式兼容，允许替换契约、迁移全部调用者并删除旧路径；存储可明确拒绝旧版或单向迁移，不默认兼容层/双写，也不猜测旧证据或清空未决资源账。

新顺序：Bun指导R3先定位和修正；Process由B→C→A调整为契约先行A→C→B，A同时定义B的小预算拒绝和C的写入成功边界。GAP-085/159/140分别建立可交付支线；Trace拆成Opening、实际范围消费者、compaction/reanchor、Fission四包；Ownership单列实际Bootstrap，再做脱组原型和平台/容量实测。各包写明目标、依赖、删除条件、真实消费者/冷恢复/物理验收，关键跨包由非作者复审；本轮未认领生产编辑面，未把计划标成实现完成。

本轮只改文档，未改生产、测试、库、vendor或用户数据，未部署Host，也未重跑业务套件；上批2545项及Bun5项超时仍是现有证据，不因计划更新变绿。R12-U1继续暂缓。提交前核对文档链接、锚点、卡号/依赖及diff，计划不承诺旧格式必须继续运行。

## 第二十三批：当前版本深测与剩余项校正（2026-10-18）

输入干净主仓`7cf6f6fd0`、库`10bbb32 / 0.6.4`。按四模块及实际消费者选取304个编号文件，三个不重叠默认组共2545项：2375 pass / 0普通fail / 30 skip / 140 TODO；全部计划文件完成、runner exit0、进程组收尾accepted=true，使用WXS_ACCEPT_TODO明确保留未完成证明。真实OpenCode Host与Trace opaque正反编译各1/1，库Node291/291；独立构建、真实组合、Node/Bun安装包及强杀恢复42项/41pass/0fail/1Linux skip。具体命令、分组、日志和优先级见[深测报告](DEEP-TEST-2026-10-18.md)。

本轮唯一代码修正为PTY测试worker显式`execArgv: []`：初次15项Bun场景因继承Node测试钩子而在业务前退出，原失败保留；修后完整134文件为818pass/0fail。未改生产或库源码，精确主包仍为2076文件/13,375,692字节及2e0f994058540b58a1c31c2bef1dc8954ccc1b0abaf6ca4871ac2e61e2da5c30，真实pack/隔离默认入口、新鲜度、format/check均通过。没有以夹具失败否定PTY产品，也没有放宽业务断言。

当前Bun库Process53pass/4fail、Supervision12/12，历史四项差异仍在；误将Node专用SQLite六文件直接交给Bun的入口错误单列，不算六个产品缺陷，实际Bun安装消费者通过。Bun WorkRecord三个文件84/84；但prefix014整文件两次20pass/5timeout/1skip，单选1/1、末七项7/7、含同代冲突九项9/9，新增CUT-17-R3待定位，不能沿用上轮Bun25pass当作本次全绿。未延时、加skip或改生产来消除失败，不无证归于拆分或上轮修复。

Process总预算和纯契约边界两个失败TODO复现；GAP-085/140/159相邻四个失败断言分列。纠正当前表仍列待修的CUT-06串文P1，以及GAP-141“无capability仍Accepted”的过时局部描述；后者023现在2pass/1boot TODO，完整boot仍待证。三个Process P2保持B→C→A，先收窄新Bun超时的确切来源；完整Trace/Ownership产品链、旧历史和平台证明不借此关闭。R12-U1继续暂缓，未部署用户Host。

## 第二十二批：CUT-17-R2 指导入口独立复审（2026-10-17，已验收）

从干净 `7c50ed590` 对比 `2aaaf6077`。非作者确认R1新增四个P1：空白重放叠加、跨代原文误报歧义、g1首次捕获借旧代误删新编辑、g0物理编辑被还原并绕过原件冲突。均有旧精确包正控，归于本代理上轮修复；未无证据归为拆分问题。此前人工呈现wire回灌Host的O1被记P1，现撤回为review来源前提误判。最终删捕获前还原，物理原件直接typed capture；Pair/Grounding只负责已确认原件上的呈现重放。新增13项正式先红反例，末尾资格/当前可见缺证拒绝和当前捕获代绑定收紧，公式由Trace owner拥有；不回退、不移位、不改codec/持久格式/库包。网络竞态、README残留、R2试制错误、时序遗漏和工具relay裁剪的空真分别记录，未通过放宽正文断言消除失败。

595/598及59b77包只保留阶段证据，未覆盖完整物理编辑边界，均未提交。最终生产600：核心Node/Bun各25 pass/0 fail/1 integration skip，真实Host1/1，同代编辑冲突、新代2074/2074原文和独立冷读通过；Trace opaque正反独立编译1/1。最终三组1363项/1312 pass/0普通fail/12 skip/39 TODO，144文件全部完成、监管收尾accepted=true。工具先Opening后加read防止relay空真；007保持全wire等式，以公开呈现owner冷重放；C36保留协议input与整份输出幂等，只还原fixture已知物理output作为下轮输入，完整Host组重跑转绿。静态/格式门禁通过，不增豁免。

精确主包2076文件/13,375,692字节，SHA-256 `2e0f994058540b58a1c31c2bef1dc8954ccc1b0abaf6ca4871ac2e61e2da5c30`；真实pack/独立默认入口与非作者Node26.6/Bun1.3.14各12组暖/新OS冷读全部通过，只用包内wanxiangli0.6.4。安装与包前后字节不变、目录已清理；相对f047与59b77仅四个预期dist模块变化。三名非作者源码/行为/证据复核完成，详见[报告](GUIDANCE-INGRESS-2026-10-17.md#r2-review)。CUT-17-R2限定范围DONE、GAP-108入口范围重新闭合，随本原子提交正常推送master；R12-U1继续暂缓，旧错历史不迁移，完整CUT-17和其余GAP不借此关闭，库0.6.4未改，未部署用户Host。

## 第二十一批：CUT-17-R1 指导入口核查（2026-10-17，后续回退见第二十二批）

输入干净 `2aaaf6077`。实际 OpenCode Host 证明指导呈现使用副本，工具原件和Trace没有回流；原直接 wire→Trace TODO的来源前提不成立，已换为保真负控和实际Host证明。另确认输入已有的P1：用户引用既有指导的完整包装会被呈现器误删，Host持久消息/Trace仍完整；现按Host part原文与对应durable occurrence精确识别附加字节，保留字面引用，未改Trace保真规则或历史事实。

Fable588，Node/Bun核心各12 pass/0 fail/1 integration skip，真实OpenCode1.18.29的网络/持久消息/后续请求/独立冷读1/1，Trace接口正反独立编译1/1；三个不重叠默认组1350项/1299 pass/0普通fail/12 skip/39 TODO。既有临时目录别名、旧歧义夹具及C36配置前提均与旧精确包对照后修正，保留原断言；本轮夹具身份、opaque句柄、最终wire重送和Bun启动HOME的问题另列，不算产品回退。格式与完整静态门禁通过，未增加豁免。

精确主包2076文件 / 13,375,004字节，SHA-256 `f047aa1c23adfe1a9602f64aae05e59b50c55b6f90bd7a503da020557d1ae70b`；真实pack、独立默认入口和同包隔离Node/Bun实际捕获/呈现/重开/重放消费均通过，安装文件与archive前后未变。CUT-17-R1限定范围记DONE，GAP-108包内oracle闭合，随本原子提交正常推送master。完整归因、命令和证据见[本包报告](GUIDANCE-INGRESS-2026-10-17.md)。下一包为CUT-02-R6-B/GAP-092；库0.6.4未改，旧错历史不迁移，GAP-101/109及其余指导义务未整体关闭，R12-U1继续暂缓，未部署用户Host。

## 第二十批：CUT-06-R4 完成表示独立复审（2026-10-17）

从干净 `49b64879c` 复核，三名非作者确认并发晚摘要、晚原件、同身份/同终点并发完成的原有缺口未闭合；原 R2 的线性历史与旧 blob 消费证据保留，不能扩大成所有并发冷恢复证明。修正显式父边因果成员、Trace 终点、WorkRecord 摘要替换关系、多 head 写入及完成歧义，另修正本轮候选的 coverage 洞和未证完成阻断后续范围两项回退。原问题、候选回退和 review 误判分别记录在[报告 R4](WORK-RECORD-HISTORY-2026-10-17.md#r4-review)。

新增确定性反例先红；本轮未提交候选还拦下 standalone Opening、同证据重复完成、未证重放扩界及 eager clock 成本问题，均已修正或移除，不混记成原输入缺陷。生产generation582：三组默认1311个不同用例 / 1195 pass / 0普通fail / 40 skip / 76 TODO；Bun核心92项/91 pass/0 fail/1 TODO。真实checkpoint前后物化及独立冷读、Trace opaque正反编译各1/1，Completion owner的94源独立Fable编译通过。三名非作者最终矩阵、真实Sync/Fork及新进程、旧v2格式和真正旧R2二进制发布blob的升级消费通过。五组Kernel白盒探针另列调查证据；其曾越界加入正式测试，已按JS门禁撤回，不计正式通过数，未增加豁免；最终完整check、format通过。

精确新包2076文件 / 13,366,102字节，SHA-256 `22998049cad15b0b4cf580c87def35f95b0656b55835fc775d4e3607f7f4176c`，真实pack和隔离默认入口通过；非作者同包独立离线安装，Node/Bun实际Sync/Fork暖交付与新进程冷读逐字相同，安装文件和包摘要前后不变，临时目录已清理。本包限定范围进入DONE，随本原子提交正常推送master。旧缺边历史不猜来源，旧v2完成blob不迁移，GAP-111/109不整项关闭；库0.6.4未改，R12-U1暂缓，未部署用户Host。

## 第十九批：CUT-06-R2 已完成范围的历史表示（2026-10-17）

从干净 `2ec1a5400` 继续，库 `10bbb32 / 0.6.4` 未改。新增纯 WorkRecord 完成表示投影：在已验证 Terminal 成功折叠的同一次 aggregate 更新中保存其 ProviderRun、确切终点和当时的不可变frame引用；不缓存任意range、复制正文或coverage、不向Trace塞Blogger状态。后续entry/squash/Terminal/reanchor与冷回放不重选表示。Sync/Fork改用该run的Terminal终点，修复完成通知/消费前head增长的既有越界；旧精确包实际Fork混入FUTURE_DETAIL的红例已保留。

Fable生产574、文档/测试元数据575；核心Node/Bun各70项 / 69pass / 0fail / 1TODO。三个不重叠相关组1239项 / 1137pass / 0普通fail / 34skip / 68TODO；新增owner94源声明闭包独立Fable编译通过；原Trace正反编译、非空checkpoint各1/1，Host接线smoke20/20。非作者独立验证真实Fork完成、后续squash、Join与新进程冷恢复，并验证空frame、重复/冲突Terminal和同终点多run。首次ratchet失败经旧新闭包对照确认只增加纯CompletionProjection.fs（391→392），按WHAT-028同步记录，未放宽60%硬上限或隔离要求。

修正前轮验收表述：Fork旧测试settle接缝缺生产Terminal捕获，旧完成blob/Join稳定证明成立，但不能单独证明Terminal事实已存在；本轮已补真正的捕获顺序。修复前已有问题、本轮编译/夹具失败和review表述误判分别列入[本包报告](WORK-RECORD-HISTORY-2026-10-17.md)。旧eeaa精确包经实际Fork生成的完成blob升级冷consume逐字保留；Terminal后才摘要/发布的旧blob缺range关联，与Terminal时刻派生视图不自动迁移或强行统一。精确新包df0ccd、2076members / 13,336,942bytes的真实pack与隔离默认入口通过；非作者同包独立离线安装、全文件字节及Node/Bun暖冷消费者四项probe均通过，format/check/diff通过。

本包所列已验证Terminal完成表示与消费者进入DONE，随本原子提交正常推送master；GAP-111/109、Opening/Resume/Fission完整链及父卡仍保持原边界。R12-U1继续暂缓；未部署用户Host。完整交付及包外证据见本包报告。

## 第十八批：GAP-111 独立复审及 Terminal 去重回退修正（2026-10-17）

从干净主仓 `e7a150937` 复核，库 `10bbb32 / 0.6.4` 未改。三名非作者分别检查范围/coverage、真实消费者/借用寿命、契约/完成blob。两项bounded P2已修：**e7新引入、前轮review漏检** 的部分摘要同源Terminal重复（18条在原566为11pass/7fail）；包外另发现输入已有的完整raw配仅formal Terminal重复（8条在568为4pass/4fail，至少53源码已有）。现在先比实际gap/最新turn末条formal，再以本轮已验证非空自然suffix与完整turn精确证明去重；较早正文同字、尾tools、空白suffix及不同Terminal仍保留应有正式陈述。未裁切/包含匹配Terminal、未回写coverage或新增持久owner。

生产最终570、测试元数据571；核心Node/Bun及非作者各 **58 / 56pass / 0fail / 2TODO**。三个不重叠默认组 **1227 / 1124pass / 0普通fail / 34skip / 69TODO**，另host-boundary-021为20/20，独立Fable编译与非空Trace checkpoint两个integration各1/1（不与默认组重复相加）。非作者每runtime另验224暖视图和224新进程冷视图，真实Sync/Fork的caller charge、后续澄清、正文与正式陈述边界及借用寿命通过。分支嵌套门禁与新增测试Task回调夹具的初次失败均保留，未放宽产品或门禁；最终静态、格式通过。

精确包2075members / 13,326,481bytes，SHA-256 `eeaa8b243ed2da6e292bd87d549ed5a810d3e75820c39310dc108df4e7f4d92f`，真实pack、隔离安装和默认入口通过。非作者同一包独立离线安装、成员字节及依赖边界核对通过；Node/Bun各14材料组、真实Sync/Fork双轮及3个子进程冷读逐字相等，formal-only原例转绿，安装及临时Git/journal已清理。170b中间包失败与CJS解析误判保留，不冒充最终证据。R3随本原子提交正常推送master。完整日志、归因和剩余边界见[复审报告](WORK-RECORD-RANGES-2026-10-17.md#independent-review)。

R2不能再仅写缺证据：真实Terminal/HandleCompleted之后，后续squash使同一完成range按当前投影重物化的表示改变，WHAT-004这一项是已确认的既有缺口；完成blob、同进程真实Join、冷解码及持久consume保持冻结。新进程Join按原归属契约排除旧handle，不冒称它已消费。GAP-111仍PARTIAL，R2按消费者分包计划继续；R12-U1暂缓，未部署用户Host。

## 第十七批：GAP-111 bounded 取材修复（2026-10-17）

从干净主仓 `53cba28de`、库 `10bbb32 / 0.6.4` 继续 CUT-06-R1。生产 bounded 路径只采用完整落在本次工作范围内的连续 frame 前缀，以实际采用材料决定 raw 起点，不再借全会话 coverage 跳过原件。保留最后助手正文；Terminal 仅对实际展示的 raw 去重。原跨界反例和无/有 Terminal 的尾部工具吞正式陈述反例均在原实现失败，现已修正。

真实 SyncDelegate、Fork resume/Join 通过 scoped JournalHandle 在实际 writer 提交观察；回调成功或抛错均撤销借用，原 owner 继续可用。未参与修改的 `gap111_review` 在固定563产物独立 Node/Bun 验证源边界、真实消费者、借用寿命、正式陈述和独立新进程冷恢复，未发现本包新的阻断问题。两处旧正控曾把 caller+assistant 聚合摘要用于 assistant-only 范围，现拆成两个真实 frame，保留 Chronicle/Terminal 正控及跨界负控；非作者确认这是夹具前提修正。

三个不重叠相关组 **1199 / 1096 pass / 0 普通 fail / 34 skip / 69 TODO**；最终核心004/010/011在Node/Bun各 **30 / 28 pass / 0 fail / 2 TODO**，不与大组重复相加。生产构建563，元数据564；format/check通过，独立编译与非空Trace checkpoint两项integration各1/1。精确主包 **2075 members / 13,323,845 bytes**，SHA-256 `ce6e084c2f0f6038edd5ad483bdbc62cd86e8ae0f5dd552caa3676335a495be1`，真实pack、隔离安装与默认入口通过；包外复核及交付结果见[完整报告](WORK-RECORD-RANGES-2026-10-17.md)。原始失败、工具/夹具误判和新证据均保留。

同一确切主包的非作者隔离离线安装、逐字节核对及Node/Bun包外探针均通过，未借主仓runtime，临时安装已清理。本包进入DONE并随本提交正常推送master；GAP-111仍PARTIAL：固定range的当前派生表示可随squash由摘要变原文，这不证明已发布完成记录被改写，后续CUT-06-R2补实际冻结与消费证据，顶层TODO保留。GAP-101/108/109、CUT-06整卡及R12-U1的边界不变；库包未改，未部署用户Host。

## 第十六批：T-01 后剩余项盘点与排期（2026-10-17，仅规划）

输入干净主仓 `c3689035c`、库 `10bbb32 / 0.6.4`。四个原 P1 子包的已验收结论保留；本次未改生产实现、测试、依赖或库包。重跑现有 work-record-004、prefix-stability-014、process-execution-012/015，得到 **17 / 12 pass / 0 普通 fail / 0 skip / 5 TODO**，日志 `/private/tmp/post-t01-plan-baseline-20261017.log`。四个 TODO 仍执行失败：跨 invocation 摘要混入旧工作、直接捕获加工后指导、纯契约混编、256 字节预算返回 549 字节；另一个仅声明待补消费者能力。直接指导捕获反例不能单独证明正常 Host 请求会污染历史，未把 TODO 数量当作产品错误数量。

已更新[剩余计划](REMAINING-REPAIRS-2026-10-16.md#post-t01-plan)与看板：下一包为 CUT-06-R1/GAP-111 的 bounded 取材及 coverage，随后 CUT-17-R1/GAP-108 核查真实回流，再按 B/C/A 完成三个 Process P2。新卡仍 BACKLOG，尚未认领生产写集合；完整 Trace/Opening/Resume/Fission、Bootstrap/主动脱组及平台/容量证明各自保留，R12-U1 继续暂缓。修正施工指南的旧 vendor 版本与旧“下一步”入口，历史失败及验收记录原样保留。

文档候选 `node scripts/build.mjs` 通过，generation 559，174 surfaces / 838 modules；日志 `/private/tmp/post-t01-plan-build-20261017.log`。`git diff --check` 通过；不把文档构建算作新一轮行为修复验收。本批只提交上述计划、看板、进度和指南，按 AGENTS.md 正常推送 master，不强推，未部署 Host。

## 第十五批：T-01 独立复审（2026-10-17，已修正并验收）

以主仓 `97abafc4a`、库 `a636cbc / 0.6.3` 为基线，先由三位非作者分别复审，再由 root 与 capture/recovery 修正，最终由未编辑源码的 originals 独立复验。原 T-01 的 N3 身份限制、N4 双 facet 消费覆盖、N5 facet 碰撞及 N6 物理种类/归属护栏遗漏均已修正。本轮未交付候选的 N7 旧 kind 绕过、N8 wrapper 误计冲突及 N9 证据 DTO int64 缩窄分别记录并修正。既有 snapshot 来源/缺 ID 和分隔符编码问题分别记 T01-04/05/06；环境、旧夹具坐标误判与门禁漂移另记，不混同归因。

库 `10bbb32 / 0.6.4` 已通过 Node/Bun 各68/68、独立构建2/2和精确包5/5并正常推送，主仓固定 SHA-256 `e24ee33d08cb12aabd0beb4af3da8fc91eacb64cd00b89ad558eef99cda9b5f5`。主仓行为编译556，174 surfaces / 838 modules；三个不重叠相关组 **2270 / 2119 pass / 0 普通 fail / 51 skip / 100 TODO**，另两项 integration 均1/1。非作者 Node/Bun 来源各13/13、tuple各6组、超2^31与2^53实际 journal DTO/cold 均通过；精确旧包对照保留。库仍是唯一原件内存 owner，EventStore 是唯一持久载体；新结构化身份不回填旧未知字段，v3 明确拒绝旧 raw cut，生产 Unknown 条件未改。

收尾 Fable557、格式/静态门禁通过。精确主包 **2075 members / 13,320,592 bytes**，SHA-256 `add77b27a037d83d8fc2c8b95a10a19b601e8ca7720c7bd183122494bd764162`；隔离安装及 Node/Bun 仓外实际捕获→提交屏障→SIGKILL→新进程冷恢复的12条 records、head、原文、opaque身份和Terminal前沿一致。只加标注的六个PTY模块与精确旧包逐字节相同，未把 Unknown 调查混入本包。

CUT-03-R6 按本轮限定范围记 DONE，主仓随本原子提交正常推送 master，不强推；未部署用户 Host。R12-U1 按用户决定保持 BACKLOG，GAP-101/108/109/111 不销项。完整证据、原始红例及兼容边界见 [T-01 报告](TRACE-ORIGINALS-2026-10-17.md)。

## 第十四批：T-01 原件接管与消费者切换（2026-10-17，历史交付）

按剩余计划处理下一个 P1，输入干净主仓 `84ba23b2d`、库 `b6984d9 / 0.6.2`，R12-U1 保持 BACKLOG。库 `a636cbc / 0.6.3` 新增已确认材料和明确 Terminal 绑定；主仓由当前 envelope 的已验证 payload 接回同步 fold，不补缺失 run/message/Completed。Materialization、WorkRecord、Y/prefix、Strength 及经 WorkRecord 的后续消费者共用实际原件，删除描述符追加与临时重捕获。EventStore 仍是唯一持久载体，既有语义游标与范围保留，新 raw identity 明确隔离旧描述符。

修正既有 Host 去重遗漏：同身份改写明确拒绝、批内重复只分配一个游标、同工具部件的调用与结果分别保留，并按精确旧物理路径兼容旧缺失字段。另修旧 Emit 缓存反复实例化及假的 lifecycle→Terminal 测试便利入口。**本轮候选引入的 Replayed record 重复追加、工具 facet 历史兼容遗漏分别记 T01-N1/N2**；null/undefined、SIGKILL/exitCode、证据视图 body 等 oracle 误判另记，不归给原拆分。非作者先证明重复前缀损坏，再验证拒绝、独立分支、缓存、旧字段保留和冷恢复。

生产 Fable **547**，文档/夹具元数据 **548**，收尾 **549** 同包摘要再核对通过，174 surfaces / 838 modules；格式、静态门禁和 diff 通过。两个不重叠相关组 **1290 / 1207 pass / 0 普通 fail / 34 skip / 49 TODO**。另启用 integration tier，独立 F# 消费者正反编译、真实非空 Trace/Terminal 的 checkpoint 保留均通过。库 Node/Bun 各 **67/67**，拆除构建 **2/2**，实际安装包 SQLite 强杀恢复、类型正反及拆除消费 **5/5**。

精确库包 SHA-256 `42eb5ca5aeb9b613ea5400ab43b4de835605e7b74d232f31db4f8e21b04e6162`；主包 **2075 members / 13,291,249 bytes**，SHA-256 `918cf931babe942b2d9f1306d1e1cbc99c5ee98c6add32d8cc0ae1dcb19e0c7b`。隔离 npm ci、默认入口、Node/Bun 包外实际捕获→SIGKILL→新进程 cold replay 的完整 11 条 records/head/正文/范围/Terminal 前沿一致，未借兄弟源码或主仓 node_modules。只有坏 durable fact 的已接受 Trace 前缀保持不动，不声称原物理日志零追加：坏 fact 和 cut-tail 仍按原 Journal 契约保存并强杀进程。

T-01 范围进入 DONE；完整产品链的 GAP-101/108/109/111、Process P2、O-01 脱组边界和 R12-U1 不销项。库正常推送既有修复分支；主仓正常交付 master，无强推，未部署运行中的 Host。[本包问题归因、逐项进度、完整命令及验收证据](TRACE-ORIGINALS-2026-10-17.md)。

## 第十三批：发布失败证据与本轮验收（2026-10-17）

从干净 `266f0c8eb` 继续 R12-U1 分层调查，确认 **R13-01/P1，R6-C 原实现缺陷**：真实内层 onExit 后，外层 terminal 发布异常会被 listener 隔离吞掉，内层目录随后正常清理，custodian 仍活却没有 terminal/fault。正式 Node/Bun 八个原反例全部失败。现先复制本次原生 code/signal/stopped，显式报告发布失败（包括 fault 文件也失败），只允许通过原实例认证且独占的 recover 重发布同一事实。待发布期间禁止控制，不重发命令；owner 丢失且仅有 signed pending 仍留账。

独立复查另指出本轮未交付候选的退休遗漏：目录 fsync 与首次正式回执读取先后失败，冷恢复成功后 owner 仍常驻；首候选十二项 **10 pass / 2 fail**。现 100ms 仅观察正式回执，验签并匹配原生事实后退休，不自动重发布。最终十二项物理反例和六项真实 Host/SQLite 用例 **18/18**，保留原 creator 的失败、退出码和停止标记。非作者另做六项实际 COMMIT 前/后 Unknown 及 ownerlost+pending 组合，全部通过。

Fable **generation 540**、174 surfaces / 838 modules；三个不重叠套件 **1232 / 1169 pass / 0 普通 fail / 16 skip / 47 TODO**。format、实际 check 和 diff whitespace 通过。精确包 **2075 members / 13,275,545 bytes**，摘要 `3241a9414740371febe870a6f36bd521d3122dd99ef4c96b313408df9be60d07`；隔离 npm ci、默认入口、Node/Bun/OpenCode 实际 PTY 消费全部通过。首次包验证的旧后代断言失败来自临时夹具跨运行共用 PID marker，已隔离文件名重验；PtyScope.js 与旧精确包 SHA-256 完全相同，未据此改产品。

**用户决定本轮验收通过，原 R12-U1 暂存待后续处理。** 40 次内层保全复验与固定并发 2 的六例分层对照均未自然复现；SIGSTOP 彩排实际执行 Unknown 捕获、sample 和延后 10s 观察，证明取证路径可用，不能唯一解释原 441 行。看板 R6-C 已验收，R12-U1 另列 BACKLOG；O-01/GAP-133 整体、Trace 及既有其余边界继续开放。独立库/vendor 未改，未部署用户运行中的 Host。详细归因、失败日志和验收证据见 [本轮报告](PTY-RESTART-RECOVERY-2026-10-16.md#r12-u1-分层取证与本轮验收2026-10-17)。

## 第十二批：R6-C 复审与修正（2026-10-16）

从干净 `9a89db96a` 对照 `81cb278a5` 复审，确认并修正五项：**R12-01/P1**（creator 越过未读输出完成）、**R12-02/P2**（恢复途中有效 NotStarted 误报 Unknown）、**R12-03/P2**（原 Host 无法收束即时或迟到的确切未启动）、**R12-04/P2**（一个本地失败阻断其他旧实例恢复）、**R12-05/P2**（真实 spawn 失败留下空端点）。均为 R6-C 新增问题，不归给原拆分。正常完成沿有序流交付输出；NotStarted 保留独立类型和原因；只有确认 Drain 才恢复无本地绑定的旧实例，本地输出故障和 Unknown 不被物理回执掩盖。命令不重发。

三名获授权的非作者分别验证协议身份、Host/Ownership 和观察，536 的九个入口场景、十二例持久边界、三个端点/慢流探针通过，所审范围未留已确认 P1/P2。正式 003 原反例 **1 pass / 7 fail**、修后 **8/8**；本地/混合七项原 **2 pass / 5 fail** 转绿，另加两项 stalled TERM 负控。本次无界 await TERM 的草稿风险在构建前被独立指出，最终沿同一 grace deadline 有界等待，不归给原交付。

另修 **R12-06，81cb 已有夹具缺陷**：Fable Exception 无 stack，worker 的失败被当 undefined 成功；新增真实 close 断言先 **0/2**，载荷和父端守卫修后独立 **2/2**。一次聚焦 **24/25** 的旧回执前置失败，已用实际 SQLite PRAGMA 锁与显式 exact close 重试定因；不增加超时或放宽 Unknown。相关夹具六项 **6/6**。Bun builtin 钩子、旧 onFault 预期、嵌套门禁和初版红测清理问题均单列，不冒充产品缺陷。

最终生产 clean **537**；不重叠回归 **1214 / 1151 pass / 0 普通 fail / 16 skip / 47 TODO**，其中夹具修改后的完整 Ownership 链 537 **558 / 529 pass / 0 / 5 skip / 24 TODO**，Process/provider 在同生产字节 536 通过。精确包 **2075 members / 13,272,635 bytes**，摘要 `63de43ac854bf7958627cb1cbde66f51a3584e2ac9cd5c5989e82aa5c0c290b3`，隔离消费、Node/Bun/OpenCode 原监管及新观察均通过；537 重包字节相同。Linux 当前物理口七接管加八观察场景通过，未冒称完整 Host/SQLite。format/check 通过，独立库/vendor 未改。

132 轮诊断另有一次缺少内层保全的物理 Unknown，blocker 保留；**R12-U1 保持 OPEN**。有内层观察的后续 Node/Bun 各 20 次均确切结束，但未复现不等于定因或修复，因此本包仍 REVIEW，报告已列保全、分层对照及正式反例验收计划。不关闭 O-01/GAP-133 整体、Trace 或其他剩余边界。[完整归因与验收证据](PTY-RESTART-RECOVERY-2026-10-16.md#独立复审与修正2026-10-16)。

## 第十一批：跨重启物理接管 R6-C（2026-10-16）

输入干净主仓 `81cb278a5`、库 `b6984d9 / 0.6.2`。先以真实 PtyScope 验证独立 owner 原型，再接回 Process/Host/ToolRuntimeScope/Load；没有修改独立库或 vendor。不可覆盖的准备记录绑定 exact lease/incarnation，私有连接只允许一个控制者；creator 才可首次启动和交互，恢复端仅查询与 TERM/KILL。重启不重发命令或输入，只有原生退出且监管范围结束的确切回执、或物理 owner 明确封存的 NotStarted，才尝试持久清账。

原产物两个真实重启反例 **0/2**，证明原 O-01 缺物理 binding；本轮候选的 TCS.TrySet 调用和恢复 signal 权限遗漏另记 **R11-01/02**，交付前已修，不归给原拆分。[报告](PTY-RESTART-RECOVERY-2026-10-16.md)保留红/绿、编译失败、测试夹具与工具失败的归因。正式 025 **103/103**，新增十三项涵盖 Node/Bun/冷 Load、Ended COMMIT 前后 SIGKILL、Reserve 后尚无准备、creator/custodian 同失；证据不够时保持原 blocker，不以 PID 消失清账。物理口七场景还验证认证、旧 incarnation、活控制连接、禁止重放、普通信号拒绝、proof 破坏、正常交互及 custodian 真正退出。

最终 clean **529**，相关不重叠回归 **1189 / 1126 pass / 0 普通 fail / 16 skip / 47 TODO**，format/check 通过。精确包 **2075 members / 13,264,745 bytes**，SHA-256 `e0764eb324842a545c0a9292b1f3b88a0153892d2fc46b30446eafb0e23b1b14`，真实打包/隔离安装、默认入口及包外 Node/Bun/OpenCode 可执行宿主通过。Linux x64/Bun 1.4.2 也用相同正式 helper 通过七场景；只证明物理口，不冒称 Linux 完整 Host/SQLite。详细命令、原始日志路径和精确字节身份见本包报告。

收尾扫描另发现 owner-lost 旧负控夹具没有清理新增 custodian；补齐确切测试 PID 的清理与等待，再跑正式 025。未改生产 Unknown 语义，也不将夹具遗漏归给原拆分。两次遗留测试进程已核对命令行后清理；包摘要复核的沙箱 npm 缓存写入失败另留日志，获原测试所需权限后同字节复核通过。

本轮为作者施工自验，R6-C 进入 REVIEW，待非作者复审；历史无准备记录、owner/回执同失、完整 Bootstrap、脱组进程、大规模容量和记录回收仍有明确边界。O-01/GAP-133 整体、Trace 和其他 P2 未销项。正常提交并推送 master，无强推；未部署用户运行中的 Host。

## 第十批：R6-B 的非作者独立复审（2026-10-16）

三名非作者从干净的 `5a9e51e44` 对照 `c659f9b33` 复审 AGENTS.md、准入、Reserve/Drain 与 Unknown，确认并修正三项：**R10-01/P1** 是本轮新增的停止失败跳过确切本地 PTY 清理；**R10-02/P2** 是本轮删除构造失败回收后遗留 native SQLite 连接；**R10-03/P1** 是既有 Load void 出口未停止旧轮准入。两项新增回退不归给原拆分；恢复遗漏不冒称本轮引入。停止准入失败现在仍尝试本地回收并保留原错误；恢复必须先确认同一 Ownership 轮次 Drain 且无 blocker，再写 void。库与 vendor 未改。[报告的独立复审节](PTY-RUN-ADMISSION-2026-10-16.md#独立复审与修正2026-10-16)保留反例、归因与全部证据。

正式新增反例修复前 **0/11**；非作者修后确认旧 worker 零 Spawn，真实 Bun 的 Unknown/初始化失败都回收确切 PID，Conflict 仅留下外国 blocker，Node/Bun 三入口十八构造场景通过。试制测试对 Bun 幂等 close 和 Surface 的 REFUSE 返回曾误判，已按原契约修正并保留失败，不改变产品迁就断言。

最终 clean **523**，相关套件不重叠合计 **1175 / 1112 pass / 0 普通 fail / 16 skip / 47 TODO**，正式 025 **90/90**；format/check 和 diff 检查通过。精确包 **2074 members / 13,224,524 bytes**，SHA-256 `edc4c842c8c5fd8c73f925f3fd6ea3c56d6db5d0e5a780ba381f3f319c975bbd`，隔离安装及包外 Bun/OpenCode 的真实 PTY 消费通过。非作者未发现声明范围内仍未修正的新回退，R6-B 进入 DONE；R6-C、Historical 无 admitted-work 的历史隔离、完整启动排序/强杀接管、主动脱组和 O-01/GAP-133 整体仍未闭合。永久 native close 失败仍只能显式失败，不保证释放。正常提交推送 master，无强推，未部署线上 Host。

## 第九批：每轮 PTY 跨进程准入（2026-10-16）

CUT-04-R6-B 已实现 canonical admitted work → run scope → runtime 子范围的接线；真实终态先持久停止该轮准入，再处理本地实例并核对整个轮次。工具捕获自己的 exact execution，稳定 tool-call 资源身份防止跨进程重放启动；冷空轮次也停止准入，未安装 drain 能力则拒绝完成。另修既有 SQLite 初始化失败重复 close，不改独立库或 vendor。

修复前两个真实进程反例 **0/2**；同 revision 的 Reserve/Drain 双顺序、双 Reserve、COMMIT 前后丢回执、重复调用、旧轮与其他 owner、真实 Bun PTY 的正反验证已接入正式 025。最终 clean **518**，完整相关链 **1156 / 1094 pass / 0 fail / 16 skip / 46 TODO**；含冷空轮次与缺失 drain 能力正反证明。format/check 通过。精确包 **2074 members / 13,222,190 bytes**，SHA-256 `308c1d002404260e66a5ebbcc8040b7852eebf5b922aa280ceb611f34107144c`；隔离 npm ci、默认入口及包外 Bun/OpenCode 的真实 PTY 消费通过。[本包报告](PTY-RUN-ADMISSION-2026-10-16.md)保留命令、日志、试制错误、测试误判和沙箱失败，未将它们归给原拆分。

本轮为作者施工自检，看板进入 REVIEW，待非作者复审；R6-C、脱组进程和 O-01/GAP-133 整体仍未完成。不会把“准入已停止”写成“已接管或已退出”。正常交付 master，无强推，未部署线上 Host。

## 第八批：PTY 修复的非作者独立复审（2026-10-16）

起点为干净主仓 `fbd90f4d5`；三名非作者对照 `ec37ab8c2` 审查监管、接线、宿主和证据。确认本轮新实现的 **R8-01/P1**（owner 不响应使排空无界等）、**R8-02/P1**（直接目标 Ctrl-Z 连同 anchor 暂停，KILL 失效）、**R8-03/P2**（Darwin/Bun 将目标 USR1/USR2 错报 PWR/SYS），均已修正。另修 **R8-04：既有测试 marker 写读竞争**；不把它算成产品回退。原 leader-exit 缺陷仍属原 O-01，三项新实现问题不归给原拆分；未确认 review 误判。详见[独立复审、归因与证据](PTY-SUPERVISION-2026-10-16.md#独立复审与修正2026-10-16)。

宿主侧单调期限覆盖启动、最早未转发请求和首次 KILL 后的实际结束确认；重复请求或转发回执不能解除期限。anchor 入组前忽略 TSTP，目标 exec 前恢复默认；仅目标由 posix_spawnp/waitpid 收取真实退出状态。Ownership、Host 持久协议及 `wanxiangli@0.6.2` vendor 未改。新正式反例先 **0/4**，发现者复验后转绿，并补启动暂停、重复 TERM/KILL 与确切 PID 收尾的独立反证。

最终生产字节 clean **509**，174 surfaces / 837 modules；format/check exit 0。旧夹具两行原子发布修正后 metadata **510**：Process **215 / 210 pass / 0 fail / 5 TODO**；同生产源码的 509 归属链 **494 / 466 pass / 0 fail / 5 skip / 23 TODO**；不重叠合计 **709 / 676 / 0 / 5 / 28**。两项真实非 PTY deadline/cancel 与完整套件并行通过。509 首轮旧 marker 普通失败及伴随未定因 TypeError 的日志保留，不覆盖成成功；展平新增分支前的 check 失败也保留。

精确包 **2074 members / 13,193,634 bytes**，SHA-256 `cba299600d29c24133acf111a14368924f7fd70154f82a72a72331fc0bd1a16b`；隔离 npm ci、默认入口以及包外 Bun/OpenCode 的真实 PTY、环境恢复与后代排空通过。Linux x64/Bun 1.4.2 复用正式 helper 和当前公开 Surface 胶囊，九项物理场景并发通过，零 worker，临时源文件已删。具体命令、摘要和非作者日志均见监管报告。

R6-A 在声明的控制终端会话范围内进入 DONE；O-01/GAP-133 整体、脱组守护进程、R6-B/C 和旧工具退出码呈现仍不销项。没有声称无限并发、快照原子性或 Linux 完整 Host 已验收。本提交按门禁正常交付 master，不强推，未部署线上 Host。

## 第七批：PTY 监管原型与生产接线（2026-10-16）

CUT-04-R6-A 已先重现原 leader-exit，再完成真实原型并接入 F# 生产物理口。目标退出与监管会话结束分开；本次 owner 的 scope-ended 和原生退出都确认后才发布完成。Host/Ownership 继续持久确认 Ended，不改库及 0.6.2 vendor。实现、试制错误归因、命令及平台边界见 [PTY 监管报告](PTY-SUPERVISION-2026-10-16.md)。

原型通过后接入生产，并把每组常驻 Bun 改为小型 shell。并发复验发现一次成员观测解析缺陷，结果保留 Unknown 却阻塞自然结束；归本轮试制候选，已修正并补空表/残行/缺 owner 等负控，原 O-01 结论不变。generation **501** 的完整 Process **209 / 204 pass / 0 fail / 5 TODO**，归属链 **494 / 466 pass / 0 fail / 5 skip / 23 TODO**；025 **64/64**，六个并发 leader-natural 复验全部通过。format/check exit 0，Linux 最终物理口四项再次通过。精确包消费及交付身份随后记入监管报告。R6-A 进入 REVIEW，未冒称非作者复审；R6-B/C、主动脱离控制终端的守护进程及 O-01/GAP-133 整体仍未关闭。

编译型宿主补验前的 clean 候选为 **generation 502**，174 surfaces / 837 modules。精确主包 **2074 members / 13,189,411 bytes**，SHA-256 `5d154a651a50adfbd5e319ed917f85ae31e6c73c64c65d84882fe09b6ee3c786`；实际隔离 npm ci/默认入口消费及包外 Bun 的真实 PTY worker、后代排空均通过，`/private/tmp/r7-package-delivery.log`。这些只属于该候选；没有部署运行中的 Host。

交付前又补真实编译型 OpenCode 的启动模式与环境隔离：内部 worker 使用当前可执行文件的 Bun CLI 模式，目标恢复原环境。两项实际宿主证明转绿；初版夹具仅改 JS 键的限制也已在报告分开说明。最终源码 generation **504**，Process **211 / 206 pass / 0 fail / 5 TODO**、归属链 **494 / 466 pass / 0 fail / 5 skip / 23 TODO**，合计 **705 / 672 / 0 / 5 / 28**；Linux 四项及 format/check 再通过。502 的包保留为前一候选，不当作新环境协议的最终包。

最终 clean **generation 505** 及交付 check 均 exit 0。精确包 **2074 members / 13,190,294 bytes**，SHA-256 `2e9d79e61516fdd699a0f5d78bd4eabfed43eaac93b87c2f354cdbf9dd76657d`，默认入口和隔离依赖消费通过；同一包分别在普通 Bun、真实 OpenCode 可执行文件下通过真实 PTY、环境隔离与后代排空证明，`/private/tmp/r7-package-compiled-final.log`。文档收尾后只刷新元数据并复核同一包摘要，正常提交推送 master；R6-A 保持 REVIEW，待非作者审查，不扩张 O-01 的完成边界。

## 第六批：剩余问题分级与小项直接修复（2026-10-16）

输入主仓 `c1ed871be`、库 `eef4825`，两仓起点干净。用户要求简单的直接修，较难的先定计划。[剩余问题和施工计划](REMAINING-REPAIRS-2026-10-16.md) 把四项 P1（后代监管、持久每轮准入、重启接管、Trace 原件迁移）分开，另登记 Process 编译边界、完整 wire 预算和 Bun 平台验收三个 P2 包；没有开始这些复杂包的生产实现，也不把 37 项 TODO 当成 37 个已确认缺陷。

已直接修 CUT-02-R6-V：库 Capture 006 错误注入夹具，提交 `b6984d9`。修复前 Bun 6 pass / 1 fail，停在 child.stdout；修复后从实际 stdout 的 read 取得原生流，再注入 error，验证生产监听器、失败结果、无假退出和无别名。Node Process/Supervision **69/69**、Bun Capture/Supervision **19/19**，日志 `/private/tmp/r6-node-process-green.log`、`r6-bun-capture-supervision-green.log`。仅改库测试和证明，不更换主仓锁/vendor，不重打 0.6.2。

第一次候选沿用 destroy(EIO)，Node 7/7，但 Bun 超时；原生 Bun 对照证实 stdout 已 destroyed 却无 error，故改为明确的原生 error 事件注入。它不是实际设备 I/O 故障。原测试 child 未捕获的失败和第一次候选的失败均保留，不写成产品 bug 或通过。

扩大 Bun Process/Supervision 为 **65 pass / 4 fail**，`r6-bun-process-green.log`，文件名不是成功标记。两条 env 失败和两条 stdin 断言单列 CUT-02-R6-C。原生 Node/Bun 对照分别证明 async/sync 的特殊 env 键不同、named spawn 拦截不成立、原生写回调 EPIPE/success 不同；见 `r6-native-{node,bun}.json`。未改这四条断言、未转 TODO/skip，也未宣称 Bun 全套通过。原生探针初版进程组清理失败；最终仅结束自己持有的子进程后得到上述结果，失败运行不计证据。

主仓本轮只改计划、看板与证据，上一轮 790 项结果仍归原提交；没有借文档更新宣称重跑主仓行为套件。`node scripts/build.mjs` 文档候选构建 exit 0，174 surfaces / 836 modules 链接通过，`/private/tmp/r6-main-doc-build.log`；diff whitespace 核对通过。T-01/O-01/GAP-091/092/133 保持未闭合，真实 leader-exit TODO 保留。库修改正常推送既有交付分支，主仓文档正常推送 master；未部署运行中的 Host。

<a id="regression-review-r5"></a>
## 第五批：全部修改的双版独立复审（2026-10-16）

起点为干净主仓 `86c0c9b8c`、库 `eef4825 / 0.6.2`。修复前为主仓 `7d8feff03`、库 `46d9c07 / 0.5.2`；覆盖全部 14 / 10 个后续提交。三名非作者只读审查：`regression_process_r5`、`regression_ownership_r5`、`regression_trace_journal_r5`。历史主仓使用原依赖独立构建成功，不能以当前产物冒充历史行为。详细归因和双版结果保存在[独立回归报告](REGRESSION-REVIEW-2026-10-16.md)。

| 工作包 | 本轮实现 | 验收和剩余边界 |
|---|---|---|
| CUT-02-R5 / P-04 | Spool 创建者在首次写失败时删除路径并重抛，不等 collector 得到句柄 | 新 writeFileSync 先实写再抛错用例先红：2 项 1 pass / 1 fail，append 对照通过；独立复验残留 true→false。未改变输出预算或摘要语义 |
| CUT-04-R5 / O-13、O-14 | CloseAll 逐项尝试后报首错；active ID 在 Spawn 前拒绝覆盖，见证退出后允许复用 | 非作者真实 fd 反例两路通过，未知 ID 保留、无假完成；不同 ID 健康 shell 已死；同 ID 第二次 Spawn=0。旧覆盖测试已纠正，WHAT003 明确未收尾物理实例不能覆盖 |
| CUT-04-R5 / O-12 | 真正 installRun 订阅和首 fork/reuse 均使用同一 preparation；有效性/exact token 在前，drain/物化在后，失败可重投；冷无绑定时读持久 blocker，拒绝假完成 | 非作者 held/retry/cancel/replacement/foreign Failed 及真实 Bun 通过；generation 490 冷 guard 四条独立对照通过。不是完整后代监管或重启接管 |

本轮施工失败如实保留：`r5-main-build.log` 首候选签名/返回类型错误；`r5-main-check.log` 嵌套控制门禁 4 项；`r5-main-ownership-tests.log` 新增矩阵因错误静态导出失败 6 项。没有把这些运行写成通过。fixture 还经非作者指出 DrainOwnedWork 会永久封准入，已改 AwaitObservedWork，7 项真实回调矩阵随后全部通过（`r5-terminal-matrix.log`）。

首个可运行候选 generation **489**，focused **1532 项 / 174 surfaces / 836 modules**。Process **196 tests / 191 pass / 0 fail / 5 TODO**；日志 `r5-main-process-tests.log`，含真实进程组观察，沙箱外运行避免 `/bin/ps` EPERM。`r5-main-build-fixed.log`。此后只做控制流展开、冷无绑定防错及文档收尾，最终候选须重新构建和验证，不能沿用此处旧候选身份。

当前库非作者定向复验为 Trace/Journal 85/85、Ownership 57/57、Process/Capture/Supervision 69/69、安装 Supervision/拆除增强 5/5；主 Trace integration 消费者 1/1。双版比较来自另行隔离安装的 exact 包探针，不是两版都运行上述新增套件。0.6.2 vendor 及库源码未修改，精确包 SHA-256 仍为 `d8909bebb92b01e0cd0cfa28cfdc14d0d1d89f467b7afe5e94a99efd96b0038e`。

最终源码构建 **generation 490**，focused **1532 项 / 174 surfaces / 836 modules**，`r5-final-build-fixed.log`。此前冷 fixture 的 Task 类型错误在 `r5-final-build.log` 留痕；拍快照改在 Dispose 前，completion 由真实 task continuation 观察，没有使用 Promise 的同步完成属性。`r5-delivery-format.log`、`r5-delivery-check.log` 均 exit 0，未放宽任何门禁。

| 完整相关需求分组 | tests / pass / fail / skip / TODO | 日志（均在 `/private/tmp/`，exit 0） |
|---|---|---|
| Work / lifecycle / session-ontology 及相邻归属链 | 492 / 463 / 0 / 5 / 24 | `r5-final-ownership-tests.log` |
| process-execution 全部 | 196 / 191 / 0 / 0 / 5 | `r5-final-process-tests.log` |
| Trace / WorkRecord / Prefix 相邻链 | 102 / 93 / 0 / 1 / 8 | `r5-final-trace-tests.log` |
| 不重叠文件合计 | **790 / 747 / 0 / 6 / 37** | 新增十项全通过；既有 TODO / skip 不算通过 |

复跑命令与前四批相同，仅测试数量增加：

```sh
TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs
TMPDIR=/private/tmp node --test requirements/process-execution/tests/*.test.mjs
TMPDIR=/private/tmp node --test requirements/semantic-trace/tests/*.test.mjs requirements/work-record/tests/{002,005,008,014}.test.mjs requirements/prefix-stability/tests/{009,012,014}.test.mjs
```

非作者冷恢复反例在 generation 489：实际 Scope 结算成功，却留 `pty:unbound-cold-resource`。generation 490 同探针拒绝，Finished=false / InFlight=false / delivered=0，订阅和原 blocker 保留；无遗留 stream 的正控正常完成。仓内 coldDevOpsReturn 入口也分别核对一致，四条全部 exit 0。日志 `ownership-r5-o12-cold-{before,after,empty-after,surface-after,surface-empty}.log`；没有 OS 进程，不把它算物理接管。

文档收尾后构建 **generation 491**，174 surfaces / 836 modules 链接通过；`r5-delivery-build.log`。实际 `node scripts/verify-package.mjs` 打包、解包、隔离 npm ci 和消费者全部 exit 0：**2073 members / 13,153,599 bytes**，tarball SHA-256 `4463c91e1c308880c9a4e1f13b63e2bfbf29fa9039cf7db56a74def7459fe987`；`r5-delivery-package.log`。本轮全部审查者的最终结论及措辞修订已纳入报告，没有把当前 85 项当成两版都通过，也没有把旧坏 raw cut 的自动升级写成已证。

CUT-02-R5 / CUT-04-R5 按上述限定范围 DONE，随本提交正常快进推送 master。提交后重新核对 freshness、主包摘要和远端身份；不使用 force push。历史临时 checkout 在确认无源文件改动后移除，运行日志仍在 `/private/tmp/`；没有把临时探针当成交付门禁。T-01、O-01 及 GAP-133 未闭合，没有部署到运行中的 Host。

<a id="terminal-cancellation-review"></a>
## 第四批：完全取消后的 PTY 准入

基线 `7d12e3462`；库及精确 vendor 包仍为已独立验证的 `eef4825 / 0.6.2`，字节未变。O-11 是第三批收尾时追加确认的问题，见[报告](SOURCE-AUDIT-2026-10-15.md#terminal-cancellation-review)。不覆盖第三批原始运行证据。

`independent_pty_r2` 依据核心 mailbox 的永久 IsCancelled、Fork/Join 和 ToolRuntimeScope 重建行为，独立确认旧实例应拒绝新 PTY。其公开端口反例在无/有初始 PTY 两路均观察到取消后的实际新 Spawn 和 completion=0；失败取消后的晚到结束也留下订阅。日志 `/private/tmp/independent-pty-r4-cancel-contract-red.log`。

新增四项先红：**4 tests / 1 pass / 3 fail**。三个拒绝/收尾反例失败；真实 Journal 固定 DevOps 保留分支的复用与 completion=2 正控通过，`r2-review-terminal-contract-red-final.log`。修复在 Reserve 前检查实际 `runtime.IsCancelled`，并在终结且 lease 空时释放订阅；CancelParentEpisode 捕获失败后的收束也覆盖空 lease 的早退。未把一次失败的取消尝试强行变成终态。

原 listener 用例的可复用操作改为 closeAll，恢复 completion=2 断言；真正可复用的取消另用 canonical DevOps 的持久 binding 验证。两个正控都通过，明确纠正第三批对“所有 cancel 都可复用”的误判。新 fixture 通过真实 Journal 协议建立 binding，调用实际 Runtime 的收养消息，不改内部集合或伪造状态。

`dotnet tool run fantomas --check src/Wanxiangshu` 与 `node scripts/check.mjs` 均 exit 0；`r2-review-terminal-format.log`、`r2-review-terminal-check.log`。实际 `node scripts/build.mjs` 为 focused **778 项 / generation 486**；018/025/delegation 003 合计 **78 tests / 76 pass / 0 fail / 2 TODO**，exit 0；`r2-review-terminal-build.log`、`r2-review-terminal-green.log`。025 新增四项后为 55 项；真实 Bun 故障矩阵还检查终结旧实例不能再次 fork。

最终完整归属链使用第三批同一文件分组，**485 tests / 456 pass / 0 fail / 5 skip / 24 TODO**，exit 0；`r2-review-terminal-ownership-green-final.log`。新增四项均通过；025 为 **55 tests / 54 pass / 1 TODO**。未改的 process-execution、Trace 分别沿用第三批 **193 / 188 pass / 5 TODO** 与 **102 / 93 pass / 1 skip / 8 TODO**，三个互不重叠分组共 **780 tests / 737 pass / 0 fail / 6 skip / 37 TODO**；这不是重新跑过未改的两组。

该归属链第一次执行有 1 个测试夹具失败：PID marker 已创建而内容尚未写入，`Number('')=0` 使 shared-detach 未进入行为断言，`r2-review-terminal-ownership-final.log`。将三个 PID marker 改为先写同目录临时文件、rename 后发布，保留 PID 有效性断言；没有重试业务或把失败转 skip。上述完整复验才是最终通过结果。

非作者在 generation 486 独立运行 025 **54 pass / 1 TODO**、delegation 003 **13/13 pass**。原先独立反例原样重跑：无/有初始 PTY 两路 next.ok=false、newSpawns=0、listenerAfterCancel=0；失败取消的晚到退出使订阅从 1 降至 0。另建 actual Journal 的 canonical DevOps 正控，连续两次取消后仍可复用，完成计数 **1→2→3**，最终 detach 后订阅为 0。证据 `independent-pty-r4-gen486-025-final.log`、`independent-pty-r4-gen486-delegation003-final.log`、`independent-pty-r4-cancel-contract-green.log`、`independent-pty-r4-canonical-companion-green.log`。O-11 按这两个实际状态分支的限定范围复审通过；空 lease 的 abandon 提前失败支路仅静态核对，未单独注入该故障。

PID 就绪修复也经非作者复审：三个写点均先写完整内容再发布 marker，原 PID 校验保留。独立并发运行 shared-detach **6/6**，helper 返回后另查 **12 个确切 PID 均已退出、12 个临时文件均不存在**；`/private/tmp/independent-pty-r4-marker-check.log`，脚本 `independent-pty-r4-marker-check.mjs`。没有重建或修改共享源码。

文档收尾后的实际构建 generation **487**、174 surfaces / 836 modules 链接通过；`r2-review-terminal-build-delivery.log`。真实 `verify-package.mjs` 打包、解包、隔离 npm ci 和消费均 exit 0：**2073 members / 13,142,101 bytes**，tarball SHA-256 `040e22e71cc7f8d607bb417af263ea32b6341de2d2b64af9ae05657880e3c7d3`；`r2-review-terminal-package.log`。它替代第三批主包作为当前交付候选，库 0.6.2 的固定包摘要未变。

R4 据独立复验和主包消费证据 DONE，主仓以本提交正常快进推送 master。提交后再核对 freshness、精确主包和远端一致性，最终身份在交付回复中报告。没有部署到运行中的 Host。T-01、O-01 和 GAP-133 状态不变，不将本地准入修复扩大为完整抽取完成。

<a id="independent-review-fixes"></a>
## 第三批：独立复审与 0.6.2 修复

复审基线为主仓 `bb9a811cf`、库 `f6cd8c3 / 0.6.1`，两仓起点干净。审查者不编辑共享源码：`independent_journal_r2` 复核 Journal，`independent_pty_r2` 复核 PTY，`independent_boundaries_r2` 复核跨包边界与实际构建。发现、优先级和后续计划见[报告独立复审段](SOURCE-AUDIT-2026-10-15.md#independent-review-fixes)。

O-09、O-08、J-02、O-10、E-03、E-04 的局部实现已完成。库 `eef482506f4ea65cad0a88d3a8c5769a8ca9adbb / 0.6.2` 已正常推送原交付分支；主仓固定 `vendor/wanxiangli-0.6.2.tgz`，SHA-256 `d8909bebb92b01e0cd0cfa28cfdc14d0d1d89f467b7afe5e94a99efd96b0038e`。0.6.1 留在 Git 历史，没有覆盖同版本内容。

### 反例与局部复验

- Cancel/Drain 十项新用例首次 **10 tests / 0 pass / 10 fail**；`/private/tmp/r2-review-cleanup-red.log`。真实 Bun 旧 Cancel 反例为 closeAll/detach/cancel × 提交前后六组合：无 Signal、两个 PID 均存活、两个 blocker。修复逐项保留 Cancel 错误、继续真实清理；没有返回假成功。
- Journal 关闭失败矩阵首次 **17 tests / 13 pass / 4 fail**，修复后 Journal **30/30**；`r2-review-journal-close-red.log`、`r2-review-journal-green.log`。独立关闭失败探针在 Node/Bun × COMMIT 前后四组合也通过。
- Listener 三项先红，**3 tests / 0 pass / 3 fail**；`r2-review-listener-red.log`。原生订阅可释放后，025 验证永久 detach、可复用 cancel、失败后的晚到退出与持续 Unknown 后显式确认；Process 003 验证同一公开 JS sender 多注册、重复 Dispose 只移除自己的注册。Surface 每次生成不同的底层闭包，此用例不直接证明底层函数引用相同的情况。第一次候选错误地用已 cancel mailbox 的 PendingCompletionCount 作 oracle，2 项不通过；改为实际订阅计数与 lease/blocker，未改生产语义。当时 018/025/Process 003：**83 tests / 82 pass / 0 fail / 1 既有 TODO**；`r2-review-listener-green-final.log`。其中 025 为 50 项，含受控与真实 Bun 各八项 Cancel 故障矩阵。
- 非作者先独立复验 Host 清理：真实 `node scripts/build.mjs`，focused 778、generation 477、174 surfaces / 836 modules，exit 0；随后 plan no-op。018/025 当时 **50 tests / 49 pass / 0 fail / 1 既有 TODO**；真实 Bun COMMIT 前后丢 Cancel 回执均保留 Unknown，同时 PID 退出、port/lease 清除、blocker 为空。日志 `r2-independent-host-build.log`、`r2-independent-host-tests.log`、`r2-independent-host-real-cancel.log`。此后订阅契约变更的真实聚焦构建为 generation 479、1532 项，exit 0；`r2-review-listener-build.log`。
- 库完整构建和七入口全部源码、真实组合、隔离编译及安装包套件：**320 tests / 319 pass / 0 fail / 1 skip / 0 TODO**；`r2-review-library-build.log`、`r2-review-library-tests.log`。skip 仍是 Linux 限定的基础 Process 安装用例。
- 非作者 Journal 复验 **30/30**；当前双写者用例连续 **25/25**。临时去 finalize 突变在 Bun 七种模式全部被拒绝，Node 七种仍通过；原 close/initialize/commit-after 假阳性已被保存的原生 get 断言捕获。严格 TypeScript、独立回滚/重入和显式 close 重试均通过。原始材料 `/private/tmp/journal-independent-r2.BRvunK/recheck/`；没有共享源码突变或 GC。
- 非作者对确切 0.6.2 tarball 另建隔离消费者：清除 NODE_PATH/NODE_OPTIONS 后，Node **7/7**、Bun **7/7** 和 strict TypeScript 均通过；22 个包文件逐字节等于库交付目录与实际安装内容。证据 `/private/tmp/r2-independent-exact-062-J4mBvW/`，含 `node-matrix.json`、`bun-matrix.json`；未重建共享 dist 或借用主仓依赖。

### 最终集成门禁与剩余范围

环境 macOS、Node 26.6.0、Bun 1.3.14。实际构建 generation 480 为 full impact 1532 项，接口与 836 模块链接通过。以下三个分组无重叠文件，均 exit 0；完整命令沿用第二批相同分组，没有跑无关全仓套件。

| 分组 | tests / pass / fail / skip / TODO | 日志（`/private/tmp/`） |
|---|---|---|
| Work、managed-session-lifecycle、session-ontology 及原相邻归属链 | 481 / 452 / 0 / 5 / 24 | `r2-review-main-ownership-final.log` |
| process-execution 全包 | 193 / 188 / 0 / 0 / 5 | `r2-review-main-process-final.log` |
| Trace 与原 WorkRecord/Prefix 相邻链 | 102 / 93 / 0 / 1 / 8 | `r2-review-main-trace-final.log` |
| 合计 | **776 / 733 / 0 / 6 / 37** | 零 cancelled；TODO/skip 不计通过 |

37 个 TODO 包含原 36 个及新增 O-01 的真实 leader-exit 反例。该项实际运行并因后代仍存活失败，绝不是新能力已通过。025 当前为 **51 tests / 50 pass / 1 TODO**。helper 在 finally 中只按测试持有的确切子 PID 清理，并等待真实退出；非作者另行读取两个 marker，确认两个 PID 均已不存在。`independent-pty-r3-025.log`、`independent-pty-r3-process003.log`、`independent-pty-r3-helper-check.log` 留存独立运行结果。外层夹具在 helper 成功返回后即记录已清理，避免预期断言失败后再次使用旧 PGID。

最终静态门禁首次发现 ClosePty 的 try→match 嵌套违反 fsharp-control-pyramid，未计通过。提取同一同步 Cancel 请求为私有成员后，`dotnet tool run fantomas --check src/Wanxiangshu`、`node scripts/check.mjs` 均 exit 0；日志 `r2-review-main-format-final2.log`、`r2-review-main-check-final2.log`。没有放宽门禁或改业务顺序。真实 Host 聚焦重编 generation **481 / 778 项** 后，018/025 再验 **61 tests / 59 pass / 0 fail / 2 TODO**；`r2-review-main-build-focused-final.log`、`r2-review-main-host-focused-final.log`。其它两组对应未改的实现，未把该定向重验冒作重新跑过全部 776 项。

`independent_pty_r2` 对最终 generation 481 独立运行 018/025，得到同样的 **61 / 59 pass / 0 fail / 2 TODO**，确认 O-08/O-09/O-10 及原 O-05/O-06/O-07 的限定范围成立，未发现新增问题；`/private/tmp/independent-pty-r3-gen481-host-final.log`。再次运行 helper 并读取 marker，两个确切 PID 均已退出；`independent-pty-r3-gen481-helper-check.log`。本批与 R2 的局部卡据此 DONE，原 R1 不被代替。

文档收尾后的 `node scripts/build.mjs` generation **482**，exit 0；`r2-review-main-build-delivery.log`。`npm_config_cache=/private/tmp/module-improvement-npm-cache node scripts/verify-package.mjs`，exit 0，已真实打包、解包、隔离 npm ci 并消费主包：2073 members、13,141,126 bytes，SHA-256 `856615c21f7798151c96b0b9f2b72ffd7778009d5193eb81cbda4c5e4dbce78e`；`r2-review-main-package.log`。该门禁检查主包内实际 bundled wanxiangli 版本 0.6.2，不借顶层依赖掩盖缺包。

两仓均采用正常提交与正常推送：库交付分支已到 `eef4825`；主仓本提交在 master，不强推、不改写历史。提交后再执行 freshness 与主包消费门禁，结果与包身份核对后记录在交付回复。没有部署到运行中的 Host。

T-01 / O-01 仍 DOING，GAP-133 仍 PARTIAL。本次独立真实反例确认：PTY leader 自然退出后，同组后代仍活着，而本地与持久 blocker 已空；这是完整物理监管的既有未完成能力，不能借 Cancel 回执修复关闭。下一包须保持可安全监管的确切实例，覆盖 leader 先退出与忽略 TERM 的后代；不拿缓存旧 PGID 盲杀。报告已写明验收条件。

<a id="second-review-fixes"></a>
## 第二批：0.6.1 收尾与准入改进

基线为主仓 `5dd9dec9c`、库 `3ad5b88 / 0.6.0`。本批问题及合理性分析见[报告第 0 节](SOURCE-AUDIT-2026-10-15.md#second-review-fixes)。独立库代码提交 `93a717316b376f94db7a7f4b8916d5e58d73eb8e` 已正常推送到原交付分支；主仓升级固定包 `vendor/wanxiangli-0.6.1.tgz`，SHA-256 `c998f59aa1934fcfe915e24fec750c7da6276825c9ac8c91b558a5f8925277ed`。旧包留在 Git 历史，不重写同版本字节。

| 问题 | 实现与本批验收 | 保留边界 |
|---|---|---|
| J-01 / P1 | Journal 对每个原生 statement 用 try/finally 释放；Node 仍由 connection.close 负责最终释放。新增 SqliteStatement 声明和自定义连接义务 | Unknown、零等待 CAS、历史格式不变；未证明断电、任意自定义驱动或原生 close 自身失败后的恢复 |
| O-05 / P1 | detach/cancel 改走当前实例逐项关闭；ClosePty 在物理等待后确认缓存的同一退出事实；仍提交未知就明确失败并保留 lease/blocker | 只重放同一 Ended 事实，不再次启动或假造 NotStarted；跨重启缺失的物理终态仍不能猜测 |
| O-06 / P2 | 物理清理按本实例 lease，detach/cancel 核对独立 scope；同端口、同 durable root 的其它实例继续存活 | CloseOwnedPtys 仍作根范围 blocker 检查，不能把本实例 detach 当作整个逻辑 owner 已可退休 |
| O-07 / P1 | 永久 detach 开始时锁住本地准入，Drain 丢回执也不重新允许 fork；新运行时的独立 scope 仍可使用 | 没有据此宣称跨进程、每业务轮次的代际准入已经实现 |
| B-01 / P2 | Host 项目补充 DurableWorkHost 所在直接引用；随后只改 Host 文件的真实聚焦编译通过 | 没有改构建器、隐式借用全仓源码或放宽检查 |

本段保留 0.6.1 交付时的证据：当时局部实现和自验完成，两张 R2 卡进入 REVIEW。后续非作者复审、关联缺陷及修复见上方第三批，不把原 316/754 项追写为新增证明。CUT-03-R1 的 T-01、CUT-04-R1 的 O-01 仍未闭合。本批没有发布到正在运行的 Host。

### 先红与纠正

- Journal 新矩阵首次 13 tests / 11 pass / 2 fail：Bun 的读取失败后 statement 未释放、COMMIT 入口未知后重开写入被锁；`/private/tmp/module-improvement-journal-red.log`。其余新增场景是回归，未冒称也曾失败。
- Host 的两个 Ended 丢回执、两个共享端口 detach 和两个共享端口 cancel 场景首次 6/6 失败；`/private/tmp/module-improvement-host-red.log`。
- Drain 未提交后仍允许 fork 的新增反例先红；COMMIT 后丢回执的正控原本通过，2 项合计 1 pass / 1 fail；`/private/tmp/module-improvement-drain-red.log`。
- 中间候选仍消费 0.6.0 时，真实 Bun 的 Ended-before 场景明确报告 `database is locked`；换入 0.6.1 后同一入口通过。detach 时 mailbox 已取消，PendingCompletionCount 不能作为 Ended 落盘 oracle；这里用实际 PID、lease、durable blocker 和重开读取，原有正常退出恰好一次测试继续保留。
- 一次聚焦构建暴露缺引用；另一次 Fable 编译虽完成，但我在编译期间更新了报告，改变生成输入，主构建以 `Mid-build mutation detected` 拒绝。两次均不计通过。补引用并冻结输入后，全量构建 generation 472 和聚焦构建 generation 473 均 exit 0；聚焦从原来缺依赖的 624 项扩为含所需依赖的 778 项。日志：`/private/tmp/module-improvement-main-build-final.log`、`module-improvement-main-build-dependency.log`、`module-improvement-main-build-stable.log`、`module-improvement-main-build-focused.log`。

### 验收结果

环境 macOS、Node 26.6.0、Bun 1.3.14。以下命令以相同 0.6.1 代码和已声明依赖运行；TODO、skip 仍不算证明。

- 独立库：`node scripts/build.mjs`；全部七入口源码、组合、隔离编译及安装包套件 **316 tests / 315 pass / 0 fail / 1 skip / 0 TODO**，日志 `/private/tmp/module-improvement-library-build.log`、`module-improvement-library-tests.log`。Journal 单独为 **26/26**，包含 Node/Bun 各五种真实资源收尾场景；`module-improvement-journal-green.log`。
- 精确 vendor 包另装入 `/private/tmp/module-improvement-exact/consumer`；清除 NODE_PATH/NODE_OPTIONS 后，在 Node/Bun 各跑正常关闭、初始化失败、读取失败、COMMIT 前/后丢回执，十次均成功，并通过 strict TypeScript。helper 来自已提交的 `src/eventstore/tests/recovery-consumer.mjs`；没有借主仓 node_modules，未调用 GC。SHA-256 与上述固定包相同。
- 主仓归属及相邻链：`TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs`：**460 tests / 432 pass / 0 fail / 5 skip / 23 TODO**，exit 0；`/private/tmp/module-improvement-main-ownership-final.log`。025 共 32 项，含四个新增真实 Bun Ended-before/after、shared-detach/cancel 场景，以及原真实 TERM→等待→KILL。
- 主仓进程：`TMPDIR=/private/tmp node --test requirements/process-execution/tests/*.test.mjs`：**192 tests / 187 pass / 0 fail / 0 skip / 5 TODO**，exit 0；`/private/tmp/module-improvement-main-process-final.log`。最初两项因沙箱禁止 ps 观察而未取得结论，获得对应权限后原套件复跑通过，未改 oracle 或转 skip。
- Trace 及相邻取材：`TMPDIR=/private/tmp node --test requirements/semantic-trace/tests/*.test.mjs requirements/work-record/tests/{002,005,008,014}.test.mjs requirements/prefix-stability/tests/{009,012,014}.test.mjs`：**102 tests / 93 pass / 0 fail / 1 skip / 8 TODO**，exit 0；`/private/tmp/module-improvement-main-trace.log`。后续只改 Host 准入标记、参数名和项目引用，没有更改 Trace 实现。
- 三个主仓分组没有重叠文件：合计 **754 tests / 712 pass / 0 fail / 6 skip / 36 TODO / 0 cancelled**。`dotnet tool run fantomas --check src/Wanxiangshu` 和 `node scripts/check.mjs` 均 exit 0，日志 `/private/tmp/module-improvement-format-final2.log`、`module-improvement-check-final2.log`。shard-edge-audit 的既有 report-only 提示没有改成通过证明。
- 文档收尾后的构建与主项目安装包验证单独记录在交付门禁段；不以这次源码回归代替包消费或部署证明。

### 交付门禁

- `node scripts/build.mjs`：generation 474，exit 0；`/private/tmp/module-improvement-main-build-delivery.log`。此前全量及只改 Host 文件后的聚焦编译分别证明直接引用与所选闭包成立。
- `npm_config_cache=/private/tmp/module-improvement-npm-cache node scripts/verify-package.mjs`：exit 0，真实打包、解包及隔离 npm ci 后消费，不借主仓 node_modules。2073 members、13,137,717 bytes，主项目 tarball SHA-256 `cfe28196f51118aa3038b3d0ab00f1e5a5846854c8b2f3cc5dda3b8b6809d610`；`/private/tmp/module-improvement-main-package.log`。最初沙箱网络导致 ENOTFOUND，未计为通过；允许网络后同一门禁通过，原记录在 `module-improvement-main-package-sandbox.log`。
- 主仓按正常提交、正常 push 交付 master；独立库留在既有交付分支，代码提交 `93a7173`，后续规则证明更新 `f6cd8c3`，没有改库 main 或强推。该次交付时 R2 的非作者复审尚待补，当前结果见上方第三批；这里只交付已验证代码，没有部署到实际 Host。

## 第一批交付：0.6.0（历史证据）

主仓代码提交 `ffe028797`（包含前序独立修复），库交付分支 [codex/repair-four-modules-20261015](https://github.com/dyx13/wanxiangli/tree/codex/repair-four-modules-20261015) 已推送至 `3ad5b88`。主仓固定 `0.6.0` tarball，不更改独立库远端 `main`。这里只报告代码与验证，没有部署到运行中的宿主。

| 问题 | 本批结果 | 剩余边界 |
|---|---|---|
| P-01 | 完整 Capture → Spool；写盘失败显式失败并清理 | 默认基础 ProcessSession 仍按原契约留尾 |
| P-02 | 所选非 PTY 执行使用存活组长；目标先退出后的取消/期限反例转绿 | 脱组后代、宿主崩溃接管及任意全树清除不在此承诺内 |
| P-03 | 相对 PATH 按目标 cwd 解析，原生 spawn 对照通过 | 无新增隐式环境继承 |
| T-02 / T-03 | 完整 Opening、追加晚绑定、固定 cut 与恢复通过 | T-01 原件权威仍未迁移；旧外存 raw cut 的跨协议兼容不保证 |
| O-01 | 生产持久 Reserve、首次 Committed 才启动、统一 blocker、冷读取已接通 | 部分完成；跨重启物理接管、跨进程每轮代际准入尚未承接 |
| O-02 / O-03 / O-04 | 不提前 Ended；同步完成和丢回执精确重放；查询失败不吞空；Applied 不伪装 Committed | 未观察的旧物理实例保持阻塞，不猜测释放 |
| E-01 / E-02 | Proxy 实际重入反例；生产 bun-pty 真实 TERM→等待→KILL→exit 通过 | GAP-133 整体仍 PARTIAL；其他既有 TODO 未关闭 |

最终构建 generation 467；format:check、check、真实主项目安装包消费均 exit 0。相关主仓套件共 741 tests：699 pass / 0 fail / 6 skip / 36 TODO / 0 cancelled；TODO 和 skip 不计为已证明。详细命令、分组和原始失败记录在下文。

## 本轮基线与隔离

- 主仓起点 `b440f4f5282f703c0bc2b68da7b7622c0dc0d532`，`master` 与 `origin/master` 一致，工作区干净。
- 独立库起点 `46d9c072af50389d069709e847d5772af3ec160a / 0.5.2`，工作区干净。
- Process：`codex/repair-process-p1`，主仓 `/private/tmp/vibe-fs-repair-process`，独立库 `/private/tmp/wanxiangli-repair-process`。
- Trace：`codex/repair-trace-p1`，主仓 `/private/tmp/vibe-fs-repair-trace`，独立库 `/private/tmp/wanxiangli-repair-trace`。
- Ownership 物理边界：`codex/repair-ownership-p1`，主仓 `/private/tmp/vibe-fs-repair-ownership`。组合层由 root 在主工作区串行修改、集成。
- 各树自行安装依赖并拥有产物。包版本、tarball、锁、看板及主线集成由 root 串行处理；不同时构建同一输出目录。

## 工作包与验收范围

### Process：P-01 优先，P-02 不放宽旧 PGID 规则

保留基础留尾接口，增加显式、有界的无损输出消费能力，由主仓消费字节并写 Spool。先补大双路输出、慢消费者、EOF 与失败反例；主仓停止用轮询 tail 承接完整输出。相对 PATH 修复可独立验收。Draining 的监管能力必须单独证明可靠实例身份，不能因 P-01 修复而宣称 P-02 关闭。

### Trace：T-02 / T-03 的完整 Opening 与稳定 cut

保留原首条 Opening。大材料用确认消息部件分片，通过只追加的 Opening 绑定记录指认完整材料；晚绑定不重建同名轨迹，旧 through 不读取后来绑定。先补大材料异尾、UTF-8 边界、晚绑定后旧 cut 和恢复测试。T-01 的原件权威迁移仍独立保留，不把局部修复算成整卡完成。

### Ownership：O-02 / O-04 优先，同时修同步完成窗口

取消或解除名称绑定不再直接写 Ended；Spawn 前准备预留与请求关联，不提前声明 Started，真实同步 Ended 可以结算。查询错误显式失败。内存 Applied 与持久 Committed 分开；纯本进程 callback 仅取得内存许可。生产 PTY 持久预留与跨进程恢复须另附真实接线证据，不能只用库组合测试代替。

## 验收记录

以下保留中间候选的实际记录及当时待验范围。最终精确包与扩大复验另列，不把中间通过数拼成整卡已完成。

### Ownership 组合层的第一组反例

- `env TMPDIR=/private/tmp node --test --test-name-pattern='process-local reservation|unknown ownership scope|durable blocker queries' requirements/work/tests/009.test.mjs`：原实现 0 pass / 3 fail，exit 1。分别命中 Applied 被改为 Committed、缺 scope 被吞为空、缺 durable owner 被吞为空；日志 `/private/tmp/cut04-r1-root-red.log`。
- 首次修复后 `node scripts/build.mjs`：exit 0，generation 464，Fable focused impact 1530 items；日志 `/private/tmp/cut04-r1-root-build.log`。
- 该中间候选 `env TMPDIR=/private/tmp node --test requirements/work/tests/009.test.mjs`：6 tests / 6 pass / 0 fail / 0 TODO，exit 0；日志 `/private/tmp/cut04-r1-root-green.log`。随后新增的生产持久接线与 factory 接口尚待统一构建，不引用这次运行替它们验收。

### Trace 的库内协议与主仓候选

- 原库加 OpeningBound 反例：44 tests / 40 pass / 4 fail，日志 `/private/tmp/trace-opening-red.log`。
- 修复后 `node scripts/build.mjs trace` 与 `node --test src/trace/tests/*.test.mjs`：55 tests / 55 pass / 0 fail / 0 skip / 0 TODO，日志 `/private/tmp/trace-opening-green.log`。
- 库候选 `4e83d4307f7e9eb6fd75451e9479e7ffa92fb0e6`，主仓候选 `d1abbb51ffbda01598880da13ed4d3131c81f620`，已由 root 串行集入两仓。主仓使用新包的构建及 semantic-trace / 相邻取材回归尚待执行。
- 未发现生产持久保存新库 raw cut / Bridge records 的消费者；旧 journal 保存旧事实和 blob refs。小且首条 Opening 保留旧编码；不保证旧坏版本被外部私存的 raw cut 自动跨协议升级兼容。T-01 原件权威迁移仍未闭合。

### 原报告的独立套件验证缺口

- Journal 竞争测试在发送启动消息前登记 worker 退出等待，双方正常退出后才重开；新增真实构造锁竞争并核对被拒连接关闭。没有修改 `busy_timeout=0` 或 CAS 规则。
- Process 的 cwd 比较物理目录身份，真实子程序环境与同输入原生 spawn 对照；另检查传给 spawn 的精确环境及宿主哨兵变量不泄漏，没有忽略任意多余键或跳过 macOS。
- `node scripts/build.mjs eventstore process` 与 `node --test src/eventstore/tests/*.test.mjs src/process/tests/001.test.mjs`：37 tests / 37 pass / 0 fail / 0 skip / 0 TODO，exit 0；日志 `/private/tmp/validation-build.log`、`/private/tmp/validation-tests.log`；库提交 `6acb798`。完整组合验收仍待新包固定。

### Process 无损采集候选

- 库 `77acafa`（主线集成为 `490e1c0`）：默认 ProcessSession 保留原留尾契约；显式 Capture 构造提供单消费者 `takeOutput`，每块最多 64 KiB，原生 pipe 背压限制未消费积压；`discardOutput` 是显式放弃，不冒充采集完成。
- 库 process 002–006：35 / 35 通过。独立安装包在 Node/Bun 上逐通道核对 32 MiB 输出，TypeScript 正反声明检查通过。主仓桥接的双路各 64 MiB 输出摘要一致；消费者抛 ENOSPC 后记录 failure，再观察真实 close，日志 `/private/tmp/process-repair-main-bridge.log`。
- 主仓候选 `d4164b4a0`（主线集成为 `205ebbb61`）删除有损尾部轮询；落盘/采集失败进入 ExecutionFailed，清理部分 spool，discard 后迟到输出不能重新创建 spool。正式 process-execution 回归待新包统一构建。

### 独立库中间整合回归

- 输入为 Trace、Capture、验证缺口三项集成后的库 `490e1c0`，尚不包含新的监管器。
- `node scripts/build.mjs` 及源码报告列出的完整原有测试命令：286 tests / 285 pass / 0 fail / 1 skip / 0 TODO / 0 cancelled，exit 0。skip 是仅在 Linux 运行的 process 安装包拆除测试；日志 `/private/tmp/p1-library-interim-build.log`、`/private/tmp/p1-library-interim-tests.log`。
- 原库仓只有读取权限，已在用户账号建立公开 fork [dyx13/wanxiangli](https://github.com/dyx13/wanxiangli)。后续源码推送到该 fork；原仓保留为 upstream。当前仍是集成候选，不把本次中间运行替代最终 tarball 和主仓验收。

### Process 监管与真实反例

- 主仓修复前运行 `node --test --test-name-pattern='terminates the group after the target exits' requirements/process-execution/tests/{004,006}.test.mjs`：2 tests / 0 pass / 2 fail。目标已实际退出，取消和硬期限都未终止同组孙进程；测试自行清理后代，日志 `/private/tmp/p02-host-red.log`。取消沿用既有异常契约，测试不要求它返回 ProcessCancelled 值。
- 独立 `SupervisedProcess` 保留真实组长身份；以基础 ProcessSession Capture 运行交付的 F# bundle，再以同组目标的启动、退出及输出关闭事实回答。目标退出与监管器退出分开，未观察目标退出时保持 UNKNOWN_TARGET_EXIT；基础对象仍拒绝使用旧 PGID。
- 监管库候选 `086ec780541dec32d3b94e31360d48e403cb9029`（主线 `4ec9bdc`）：Node 12/12；Bun 逐 001–005 共 12/12；strict TypeScript 与独立 Fable 构建通过。日志 `/private/tmp/supervision-node-green.log`、`/private/tmp/supervision-bun-001.log` 至 `005.log`、`/private/tmp/supervision-build-final.log`。
- Bun 曾在清组后卡住 Capture EOF。原生 probe 证实 readable 缓冲已空且 ended，但尚未发布 end；补一次零字节 read 推进真实 EOF，不伪造 close。修复 `892f0e6`（主线 `d6b45b0`），监管原反例转绿。新增 Process 转发/KILL 用例在旧版也可通过，只算回归覆盖，不冒称红绿证明。
- 主仓只在本实例 KILL 返回 Requested 且缺目标 exit 时，保留取消/超时结果；其余控制、输出、清理错误仍进入 ExecutionFailed。目标信号按本平台原生编号映射为 `128 + signal`，不再把任意信号都报告成 137。
- 此范围不包含 PTY、脱离进程组的后代、宿主崩溃后的接管或任意进程树清除证明。监管器意外死亡后的 Closed 不能证明目标已死。

### 精确交付候选

- 库源码 `a736920`，版本 `0.6.0`；固定包 `wanxiangli-0.6.0.tgz`，SHA-256 `56c80e4bf63b999e47b021b22a6bcac3cc0d1054243d4b63a087efaf60e09b19`。主仓通过 vendor 与 package-lock 安装，未使用 npm link 或兄弟源码路径；旧 tarball 由 Git 历史保留。
- 同一库源码 `node scripts/build.mjs` 及完整源码/既有安装套件：299 tests / 298 pass / 0 fail / 1 skip / 0 TODO / 0 cancelled，exit 0；日志 `/private/tmp/p1-library-final-build.log`、`/private/tmp/p1-library-final-tests.log`。skip 仍为仅 Linux 的基础 process 安装包测试；新的监管独立安装验收另记，不用 skip 替代跨平台证明。
- 主仓旧生成缓存含已废弃 WorkspaceSnapshot 引用，会被源码扫描扫到；已将整个旧 `src/Wanxiangshu/.fable-build/output-compile` 移存 `/private/tmp/vibe-fs-stale-output-compile-20261015`，未修改或放宽门禁，未删除源码。主仓从当前输入重新构建后再报告门禁结果。
- 推送前 fetch 发现 fork 的 `origin/main` 已含 `81fef75`、`8422617` 两批新模块（0.7.0 / 0.8.0），与报告基线分叉。本轮不重写该 main，也不将这些未纳入本轮验收的模块合入 0.6.0；独立库交付分支为 `codex/repair-four-modules-20261015`，主仓仍正常快进推送 master。

### PTY 持久接线、退出及恢复

- 主仓候选 `ad3356b7bdb04f8a442c55dc825e9d9f802c96bd`（主线 `4b363f858`）统一生产 registry 为 `fork-pty:<session>`，根 scope 为该 session，运行实例使用唯一子 scope。默认真实后端缺 durable owner 时，在 Reserve 前拒绝；只有首次 Committed Reserved 允许调用 Fork。没有启动观察时保持 Reserved，不伪造 Started。
- 移除 Track/Untrack 及 Join 中的合成终结。同名绑定败方调用实际 ClosePty 并等退出；TERM 请求、空 Close 和未知 spawn/attach 异常均不结清。只有确定尚未调用 spawn 的后端加载失败，才可报告已知失败。
- lease 与 incarnation 在调用 Fork 前登记；同步 Complete 不丢失、不复活名称。真实 Ended 在提交回执前后丢失时保留原观察，以原请求重放；不补启动或重复完成通知。
- Applied 与 Committed 保持区别；查询缺 scope、坏历史和 durable owner 缺失显式失败。可选 durable 查询仅在完全没有记录时返回 None。冷父关系匹配 HandleId **及 ChildSessionId**，两父使用同名 engineer-1 不互相认领。
- 隔离树原反例 4 pass / 3 fail / 1 TODO（`/private/tmp/cut04-red-tests.log`）；修复后 managed-session-lifecycle 012/025、work 009/044 共 55/55、0 skip / TODO，`check.mjs` exit 0；日志 `/private/tmp/cut04-final-targeted-tests.log`、`/private/tmp/cut04-check.log`。这些是原锁包上的候选证据，主仓 0.6.0 的扩大复验单列。
- 边界：重启后只恢复持久阻塞，不接管没有本机精确 port 绑定的旧进程，不猜旧 PID、不重发启动。每轮 CloseOwned 的准入锁限于本进程；跨进程每轮代际 Drain/Reserve CAS、隐藏 Host-owned 后代的父级冷恢复，仍未证明。永久 detach 只 Drain 自己的实例 scope，不关闭继任实例的根范围。

### 精确包安装与主仓验收

- 库验收提交 `3ad5b88`：`WANXIANGLI_TEST_TARBALL=/private/tmp/wanxiangli-0.6.0.tgz TMPDIR=/private/tmp node --test scripts/tests/supervision-package.test.mjs`，5/5、0 skip / TODO，exit 0。strict TypeScript 正反用例、Node/Bun 各路 8 MiB 完整字节、目标先退出后的 TERM→KILL 均通过；删除无关模块及全部源码后仍可运行，删除 supervision 后基础 process 仍可运行。日志 `/private/tmp/p1-supervision-package-final.log`。此后只新增未打包测试文件，tarball 内容与 `a736920` 一致。
- 主仓 `node scripts/build.mjs`：最终 generation 467，1532 items、174 surfaces、836 modules，exit 0；日志 `/private/tmp/p1-main-final-build.log`。
- Trace 相邻回归：`TMPDIR=/private/tmp node --test requirements/semantic-trace/tests/*.test.mjs requirements/work-record/tests/{002,005,008,014}.test.mjs requirements/prefix-stability/tests/{009,012,014}.test.mjs`：102 tests / 93 pass / 0 fail / 1 skip / 8 TODO，exit 0；日志 `/private/tmp/p1-main-trace.log`。包括新 Opening、旧 cut、恢复和 Proxy 重入断言。原 GAP-108 等 TODO 不计为证明；直接 capture 保留指导后缀的已知差异未被本轮改写。
- 首轮扩大 Process 回归的真实取消、期限及信号状态反例已通过；其两处失败是旧后端文案、旧 ProcessSession 源码接线断言。Ownership 扩大回归暴露测试结束后的排空异常，静态门禁暴露 ProcessRunner 两处控制金字塔；该轮未全绿，后续修复结果见下一节。
- 首轮全仓格式检查另有 6 个原有文件失败，修复前与 `b440f4f52` 逐文件 diff 为空。已逐项手工调整 7 行缩进，独立提交 `fcba11928`；未批量重写源码。随后及最终异步清理补丁后的 `npm run format:check`、`npm run check` 均 exit 0（日志 `/private/tmp/p1-main-format.log`、`/private/tmp/p1-main-check.log`）。

### 最终扩大复验

- 测试清理异常根因是 ToolSurface 测试夹具同步 Dispose 丢弃 scope 的 detach Task，立即关闭/删除 journal；异步 Drain 因而遇到空历史。新 018 反例先红（返回 undefined），修复公共清理接口及全部调用方 await，生产严格 scope 查询未变。提交 `e4043fe`（主线 `37ec80033`）；隔离 17 文件 132 tests：122 pass / 0 fail / 3 skip / 7 TODO，日志 `/private/tmp/cut04-scope-regression-red.log`、`/private/tmp/cut04-scope-green.log`。
- 最终 `TMPDIR=/private/tmp node --test requirements/process-execution/tests/*.test.mjs`：192 tests / 187 pass / 0 fail / 0 skip / 5 TODO / 0 cancelled，exit 0；`/private/tmp/p1-main-process.log`。004/006 真正观察目标先退出、同组持 pipe 后代终止；004 区分目标 TERM/INT/KILL 状态；005/011 覆盖完整输出、失败和相对 PATH。原 GAP-092 总结果预算 TODO 仍会报出预期缺口，不算通过。
- 最终 `TMPDIR=/private/tmp node --test requirements/work/tests/*.test.mjs requirements/managed-session-lifecycle/tests/*.test.mjs requirements/session-ontology/tests/*.test.mjs requirements/crash-reconciliation/tests/020.test.mjs requirements/delegation/tests/{003,004,006,024,026,027,032}.test.mjs requirements/participant-horizon/tests/{009,010,011}.test.mjs`：447 tests / 419 pass / 0 fail / 5 skip / 23 TODO / 0 cancelled，exit 0；`/private/tmp/p1-main-ownership.log`。没有测试结束后的 unhandled rejection；025 包含实际 Bun PTY，009/044 包含 strict scope、持久预留及冷父关系。
- Trace 分组仍为上述 102 / 93 pass / 0 fail / 1 skip / 8 TODO，同一精确库；后续修改未改 Trace 行为。三个分组没有重叠文件，总计为本批交付栏所列数。
- 最终 `npm run format:check`、`npm run check` 均 exit 0；前者实际执行、后者实际执行，非缓存跳过。静态 shard-edge-audit 的既有 report-only 提示不作测试结果。`git diff --check` 通过。
- `npm_config_cache=/private/tmp/p1-npm-cache node scripts/verify-package.mjs`：exit 0，generation 467，2073 members，13,134,990 bytes；主项目 tarball SHA-256 `003f8fb5ce71e17c3779d88d8b87fc2311439da194925cf9af566343561f10af`。实际打包、解包并在不借仓内 node_modules 的外部消费者中通过；日志 `/private/tmp/p1-main-package.log`。这是安装包证明，不是生产宿主部署证明。

## T-01 / D-02：仍未闭合的 P1 与下一工作包

本轮没有把描述符桥改名后算作原件迁移，也没有为通过验收删掉旧语义。以下是下一工作包的约束，不表示每项都有新缺陷。

| 义务 | 当前事实及接线点 | 验收边界 |
|---|---|---|
| 推理、工具及省略材料 | Trace/Capture、Materialization；正文和推理都参与现有视图 | 库保存真实字节与语义种类；Y、prefix、WorkRecord 使用同一原件视图，不能改成最后一条 assistant text |
| typed retry 排除 | Capture 按 ProviderRetryAttempt 排除正文，保留 Host 行坐标 | 在原件提交前过滤；不进入 Trace/Y/WorkRecord/prefix digest，不按文本猜测 |
| 并发捕获 | 同 session 在分配游标前串行化 | 保留异步队列；库同步重入 guard 不能替代它；失败不推进已提交 head |
| 真实身份与 generation | ProviderWireCapture、Capture；合法数据可缺 ProviderRun，也无统一完成事实 | 保留物理身份、重锚 generation、stable insertion、已有覆盖游标；缺失就明确缺失，不能每 part 虚构 Completed run |
| Terminal 前沿 | Projection、TerminalReporter、WorkRecord | 按 ProviderRun 绑定既定前沿，可含推理；同材料幂等、冲突拒绝，不替换为库 statement 的不同语义 |
| Fission / Invocation / Resume | Fission Host、Fork/RunLifecycle StartCursor | 保留原半开区间及业务归属；WHAT[011]/[012] 的 GAP-101 TODO 不被存储迁移冒充关闭 |
| 历史与载体 | 正文已在事件 Payloads 内嵌；JournalIntegration 目前丢弃该上下文 | 相同事实形状/含义可以原样重放，仅换派生 owner；改变事实词汇或增加原历史没有的来源/执行断言，按 durable-events[026] 离线导入新 event_type |

下一包 **T01：原件接管及消费者切换**：先冻结库侧“已确认材料＋明确终结绑定”协议；在 JournalIntegration 的 envelope 接口保留 payload 上下文，由同步 fold 把已校验字节交给库。统一 EventStore 继续作为唯一持久载体，不新建旁挂数据库。然后保持已发布语义游标，切换 Materialization、WorkRecord 等实际消费者，删除 Model 中描述符桥及临时原文重捕获，最后用真实 journal 验收大正文、推理、工具、Terminal、并发、retry、重锚与旧保存范围。缺 payload、摘要失配、冲突必须拒绝且不推进 head。

旧生产 journal 未发现持久保存桥的 raw cut；外部另存的旧 raw cut 不保证在更换内部协议后继续使用同 ID/游标。以上是当时的 T-01 计划快照，现已按第十四批完成所列原件接管范围，历史边界与未闭合的产品证明见本包报告，不将旧计划状态当作当前状态。
