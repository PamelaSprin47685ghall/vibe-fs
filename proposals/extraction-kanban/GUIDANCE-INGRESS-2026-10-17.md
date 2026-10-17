# CUT-17-R1：指导呈现与 Host 原件入口

输入干净主仓 `2aaaf60772d6b9ea38ba88ac172a3ef8bc3ae232`，库 `10bbb3247853653543d26eb269ac49008e1b5623 / 0.6.4`。按[核查计划](REMAINING-REPAIRS-2026-10-16.md#guidance-followup)执行；R12-U1继续暂缓。

## 依据与边界

WHAT[prefix-stability-014]要求指导只参与 provider 呈现，不能进入 XTrace、Blogger、WorkRecord 或 compaction。原件入口仍须保存业务原字节，不能按正文、NUL+BOM 或指导文本猜测来源后删除。

基线 `TMPDIR=/private/tmp node --test requirements/prefix-stability/tests/014.test.mjs` exit 0，其中直接 wire→Trace 的 TODO 仍执行失败：保存了 `file content\r\n\0\uFEFFa unique guidance sentence`。日志 `/private/tmp/g108-baseline-2aaaf6077.log`。该用例绕过 Plugin 的来源处理，不单独证明正常 Host 可达。

R1输入管道曾先通过 `canonicalCaptureMessages` 还原再捕获，后执行指导呈现；工具结果注入复制message/parts/state，不修改原始Host对象。R2发现该前置还原会误改真实物理编辑，已删除；最终由typed capture直接接收物理输入，再作呈现。仍以实际后续请求、快照、普通引用和冷读验收，不只凭调用顺序。

## 工作进度

| 工作包 | 状态 | 验收目标 |
|---|---|---|
| 真实请求与入口盘点 | 已证明所列路径 | 实际 OpenCode Host、模拟 provider 网络请求、Host 持久消息及 Trace 逐项对照；指导不回写工具原件 |
| 反例与修复 | 完成 | 用户带完整包装的指导引用确有误删；来源证明进入呈现层，Trace 保真规则未放宽 |
| 回归与冷恢复 | 完成 | Node/Bun核心各12 pass/0 fail/1 integration skip；实际Host与独立冷恢复1/1；三组1350项0普通失败，独立Trace正反编译1/1 |
| 交付 | 随本原子提交交付 | 精确包与隔离Node/Bun实际消费通过；文档、看板和剩余计划同步更新，遵守 AGENTS.md 正常提交推送 master |

## 问题与归因

| 编号 | 性质 | 证据与处理 |
|---|---|---|
| G108-01 | 原 review 反例层次不符 | 将呈现 wire 直接交给无来源的 Trace，再要求 Trace 删除后缀，违反原件保真。该输入确实保留全部字节，但未证明正常 Host 污染。现改为保真负控，以注册 Plugin 和真实 Host 的正反链路承接 WHAT-014 |
| G108-02 / P1 | 输入已有的呈现误删 | 原 `stripCapturedResult` 对用户消息绕过原文证明，直接按所有旧 marker 做 Replace。实际 Host 中4201字符的普通引用在 provider 请求中只剩2107字符；持久用户消息与Trace仍完整。`/private/tmp/g108-host-probe-baseline2.log`、`/private/tmp/g108-plugin-probe-baseline2.log`。正式两项引用反例先红：`/private/tmp/g108-formal-red2.log` |
| G108-E1 | 本轮测试身份错误 | 初稿几个隔离工作区共用同一 session 身份，命中进程级准入记录，未触达引用断言。现每个场景使用独立身份；`/private/tmp/g108-formal-red.log` 不算产品失败 |
| G108-E2 | 本轮测试组合错误 | 把最终 provider wire（含无物理地址的展示占位消息）作为新 Host 原始请求重送，等待未完成；再误用不同 opaque Journal wrapper、局部 import，一度未进入呈现器。现由原始物理消息重建真实后续请求，另以该 owner 自己的公开 journal 入口验证确切 carrier 的冷重放；没有增加测试 facade 或放宽准入 |
| G108-E3 | 本轮探针环境与 mock 错误 | 首次模拟 provider 把标题请求当作 read 请求，未发生工具执行；首轮裸 Bun 测试的 homedir 指向启动时的用户目录，sandbox 拒绝写入。分别区分带 tools 的请求、在独立 HOME 启动 Bun，不修改用户配置或产品规则；失败日志保留 |
| G108-E4 | 既有共享夹具的目录别名问题，已修正 | 默认临时目录是 `/var/folders/...`，Git返回 `/private/var/folders/...`，fixture却把两者当不同工作根。旧精确包的C36/grounding代表例同样失败；统一fixture传出的真实路径后，完整相关组转绿。只改测试入口，不放宽生产根目录校验 |
| G108-E5 | 既有歧义夹具已落后于T-01，已修正 | 旧测试要求新捕获接受同一无part call的两份不同正文，已被T-01在写入前拒绝。旧精确包同样在此断言失败。现在先验证新捕获拒绝且零追加，再经公开fact入口构造明确的旧历史格式，原来两个呈现owner的歧义拒绝断言全部保留 |
| G108-E6 | 既有单测前置状态缺失，已修正 | 单选C36时没有配置Predictor，却要求恢复只在配置后保存的字段；旧精确包同样失败。该测试现在显式设置并恢复自己的配置；新旧包单选均1/1，完整032仍61 pass/0 fail/1 skip/1 TODO |

不将 G108-01 写成已确认的正常 Host 原件污染，也不将 G108-02 归给本轮修复或原模块拆分；它已存在于输入 `2aaaf6077`。本轮未改库包、持久事实或历史原件。

## 来源与职责

最终请求顺序为物理Host消息→普通Plugin的typed capture→Trace/Companion→XWire/指导/grounding呈现→provider。原件捕获前不按正文、后缀或occurrence改写物理输入；同代变更由T-01冲突拒绝，新代合法原文完整捕获。指导注入复制message/parts/state，Host数据库没有这些附加字节；真实Host后续请求重新读取持久消息，不拿上轮provider wire当新原件。

工具结果原有的原文证明继续使用；用户 carrier 按 message、物理 part（缺失时仅用已记录的位置身份）查询 Trace 原文，再以该位置 durable occurrence 的完整附加字节做精确匹配。R2最终将查询绑定本次捕获generation，代内多候选仍拒绝；新代尚未捕获时不借旧代证明，退休occurrence不删字或阻断新原文。代的计算仍由Trace owner拥有，Host从同一次session投影传入epoch证据，不复制公式或跨snapshot取材。匹配才剥离对应owner的呈现；字面引用保持原文。普通无后缀用户材料不额外查询该证明。

Trace提供精确原文和捕获代查询，不接收指导文本、不保存第二份正文或occurrence账本；临时呈现映射不跨请求保留。旧缺来源历史不补造身份，已错写的旧事实不在本包自动迁移。完整CUT-17抽取、GAP-101/109和R12-U1不借本包关闭。

## 当前验收证据

- 生产Fable587，收紧查询条件后588；均174 surfaces / 839 modules。`/private/tmp/g108-build-{first,second}.log`，未使用 dotnet build。
- 正式核心 `/private/tmp/g108-core-node-final.log`：13项 / 12 pass / 0 fail / 1 integration skip / 0 TODO；原件、快照和独立进程对照均为实际公开 Surface。旧直接捕获TODO已用保真负控及实际入口证明替换。
- 正式真实Host `/private/tmp/g108-host-integration-first.log`：`WXS_TIER_INTEGRATION=1` 按 `actual OpenCode Host` 名称执行1/1。运行原生 `opencode serve`、真实 read、本地模拟provider；完整引用进入网络请求，后续请求保持旧呈现，Host持久消息不变，Host退出后的新进程冷读保留原文。
- 独立实际Host探针修前 `preserved=false`，修后 `preserved=true`，日志 `/private/tmp/g108-host-probe-{baseline2,first-green}.log`。正式用例另核对网络与持久/冷恢复，不以此探针代替全部证据。
- 三组不重叠的正式默认组共 **1350项 / 1299 pass / 0普通fail / 12 skip / 39 TODO**。呈现/prefix/grounding/provider：412/392/0/4/16；Trace/WorkRecord/压缩：657/633/0/4/20；Host边界：281/274/0/4/3。命令为 `WXS_ACCEPT_TODO=1 TESTS_MJS_FILES="<对应目录编号测试的逗号列表>" node requirements/verification-system/tests/run.mjs`，日志 `/private/tmp/g108-{presentation,trace,host-boundary}-verified.log`，144个计划文件全部完成，三个监管收尾均 accepted=true。TODO/skip不计通过。
- C36补齐自己的配置后，完整032为63项/61 pass/0 fail/1 skip/1 TODO，另新旧包独立单选各1/1；`/private/tmp/g108-host-032-final.log`、`/private/tmp/g108-{old-,}c36-isolation-fixed.log`。它们替换上述Host组相应用例的验证，不重复相加。Trace opaque consumer独立正反Fable编译1/1，`/private/tmp/g108-opaque-compile-final.log`。
- 旧包对照使用原229980精确包，SHA-256 `22998049cad15b0b4cf580c87def35f95b0656b55835fc775d4e3607f7f4176c`。相同五个既有测试文件在明确私有TMPDIR下为153项/148 pass/1 fail/3 skip/1 TODO，唯一普通失败是旧歧义夹具；默认TMPDIR的根目录代表例2/2失败。夹具修后旧包对应正控通过。日志 `/private/tmp/g108-old-package-adjacent.log`、`/private/tmp/g108-old-default-temp-root.log`、`/private/tmp/g108-old-fixtures-fixed.log`。此对照借主仓runtime依赖，不冒充隔离安装；新旧dist只有本轮Materialization、PairProgrammingThoughtTransform两个JS文件不同。
- 原失败组日志 `/private/tmp/g108-{presentation,host-boundary}-final.log` 保留，不算通过；错误接缝、opaque wrapper、局部import和最终wire重送的日志 `/private/tmp/g108-core-node-{second,third,fourth,fifth}.log` 保留，不归给产品。本轮没有用skip或放宽断言消除失败。
- 精确主包generation590：**2076文件 / 13,375,004字节**，SHA-256 `f047aa1c23adfe1a9602f64aae05e59b50c55b6f90bd7a503da020557d1ae70b`。真实pack、独立安装及默认入口通过，`/private/tmp/g108-package-final.log`。同一包另做隔离安装，逐个核对全部文件；Node26.6.0/Bun1.3.14实际捕获→呈现→新journal重开→引用与重复呈现均通过，使用包内wanxiangli0.6.4，不借主仓依赖。包和安装文件前后不变，临时目录已清理，`/private/tmp/g108-isolated-package-final.log`。
- 最终完整静态门禁和格式检查通过：`/private/tmp/g108-check-delivery.log`、`/private/tmp/g108-format-final.log`；JS边界无违规，不增加豁免。收尾文档/测试仅刷新元数据，生产字节仍为588；完整包闭包再次核对为2076文件零差异，`/private/tmp/g108-final-package-closure.log`，旧包解压目录已清理。

## 交付边界

CUT-17-R1按所列正常Host入口、指导引用保真及冷恢复证据记DONE，GAP-108的包内014独立oracle与真实Host证明已落地。实际Host为安装的OpenCode1.18.29，版本日志 `/private/tmp/g108-host-version.log`；本地模拟provider只控制响应，不替代真实Host、read或Plugin管道。R1交付时没有非作者复审，不能把独立OS进程证据称作非作者review；随后非作者复审及新增回退见R2。

下一包按原计划处理CUT-02-R6-B/GAP-092完整wire预算，其后R6-C、R6-A。旧无来源/错写历史不自动迁移；GAP-101/109、guidance的其余GAP-115/116、CUT-17整卡和R12-U1保持各自边界。本包库0.6.4未改，不强推master，未部署用户运行中的Host。

<a id="r2-review"></a>
## CUT-17-R2：独立复审（2026-10-17）

从干净 `7c50ed590` 对比修前 `2aaaf6077`。三名非作者分别检查来源身份、真实Host链路和测试/验收归因，根代理负责修正。两次收尾反证拦下物理编辑误删，均先暂停提交。R1的正常引用修复保留，下列四项R1回退已修正；最终生产generation600已完成所列验收，前候选595/598只保留阶段证据。R12-U1不进入本包。

| 编号 | 归因、等级 | 反例与处理 |
|---|---|---|
| G108-R2-O1 | 本轮review来源前提误判，撤回原P1归因 | 旧229980包的人工呈现wire回灌也有Trace head 3→4/4→5，但这不证明正常Host把呈现当原件。此前据此记“输入已有P1污染”不成立，现撤回。Host物理输入保真；呈现重放只由对应owner承接。旧空白缺原件时不能识别伪装wire和合法编辑，不承诺自动辨认或迁移；新carrier门禁阻止新增无证occurrence |
| G108-R2-01 | R1新增回退，P1 | 上述空白carrier在公开Pair owner冷重开后重放，R1因查不到原文而再次追加指导：2058→4116、2062→4120；旧229980精确包同输入保持一次后缀。现按同一codec核对末尾carrier资格：原始part空白保留，不移到前part、不留下新occurrence。完整Host无内容时仍可追加host-boundary-011规定的无语义占位，不把占位当原件 |
| G108-R2-02 | R1新增回退，P1 | 重锚后同message/part在g:0/g:1均可有合法原文，R1跨代扫描误报歧义。物理ID/位置来源、同文/异文均可复现；旧229980包成功。最终按本次捕获generation取材、代内判唯一；不去重正文、不删除旧代事实、不改codec或位置编号 |
| G108-R2-05 | R1新增回退，P1；R2前候选未闭合 | 新代首次捕获前仅有g:0原文，R1及前候选借它剥离g:1用户编辑后的字面正文，2074字节被捕成16。旧229980包完整保留；R1 f047与前候选均误删。真实OpenCode公开Part.update已证明同物理身份可改文。最终删除物理捕获前的呈现还原；呈现查询另显式绑定当前代，无原文不回退。此前先snapshot再Pair的跨代用例漏了首次捕获顺序，新增2项注册Host先红转绿 |
| G108-R2-06 | R1新增回退，P1 | 同g:0真实物理原件由13字符改为旧完整呈现2071字符，旧包明确冲突拒绝，R1/598却清回旧正文并绕过T-01冲突。真实原件和已呈现重入可有完全相同的地址/字节，不能继续加文本heuristic。最终删`canonicalCaptureMessages`，物理输入直接typed capture，两个身份形态都恢复冲突拒绝，business parts与Trace/head不变；Pair/Grounding自身的呈现冷重放保留 |
| G108-R2-03 | R1文档遗漏、证据表述需收紧 | 测试README仍称旧直接capture反例是失败TODO，已同步真实测试。完成/错误场景的工具事件是在原件已捕获后重送，只证明请求捕获与快照补读去重，不证明此前未捕获的结果由事件独立补入 |
| G108-R2-04 | R1新增测试竞态 | observer在transform返回前写入，首次网络断言却立即要求请求已抵达，独立实际Host复跑触发失败。现有限等待实际网络里的完整rendered字节；不放宽断言或增加任意sleep。失败 `/private/tmp/g108r-host-green-independent.log`，修后1/1 `/private/tmp/g108r-host-green-independent-fixed.log` |
| G108-R2-E1 | R2施工时构建输入改变 | 根代理在首轮Fable编译期间修改跟踪文档，mid-build mutation guard拒绝发布构建；日志 `/private/tmp/g108r-build-first.log`，不计通过。输入停止变动后重新构建generation595成功 |
| G108-R2-E2 | R2新增测试断言错误 | 首次空白用例把Host额外追加的无语义`#`占位误当原part改变，19 pass/2 fail/1 skip；现同时核对Pair原数组不变、Host原part前缀不变及唯一合法占位，多part全部原样。失败日志 `/private/tmp/g108r-core-first.log`、`/private/tmp/g108r-evidence-nine-595.log`保留，不列产品回退 |
| G108-R2-E3 | R2验收遗漏及夹具组合错误 | 物理编辑负控最初比较整份info，既有权限/model元数据变动也被当作原文变化，现严格核对完整business parts与Trace。工具fixture又把read放在Opening前，二次被relay裁掉，head不增不能证明重捕获；现先真实root transform建立Opening，再推工具，二次raw必须含read，当前工具加工副本重入明确冲突拒绝。007保留全wire等式，呈现重放改走公开Pair/Grounding owner；故意追加假身份facts的负控移到有效第三次正控后，避免污染正控 |
| G108-R2-E4 | 旧C36夹具混用来源，已修正 | 最终Host组仅C36把上次provider指导后缀当下一次物理tool output，触发正确的原件冲突。现在两次重入只设置回fixture保存的physicalOutput常量，不解析后缀，不重置已恢复input/contract/estimated字段；整份输出与键顺序断言保留。单选1/1 `/private/tmp/g108r-c36-physical-boundary.log`；非作者确认协议幂等要求未放宽，原失败组保留 |

G108-02仍是R1输入已有的引用误删，G108-01及R2-O1分别是两次review来源层次误判。R2-01/02/05/06经同输入旧包对照确认，明确归于本代理上轮修改，不能归给拆分或测试环境。R2-05/06未在前候选修正，不称R2新引入；本轮初次验收漏了首次捕获和原件编辑顺序则单独负责。未凭空把新缺陷归为原拆分；普通Host身份互异由准入合同保证，未据非法重复ID增设兼容层。

反证日志：`/private/tmp/g108r-blank-host-reentry.log`、`/private/tmp/g108r-multipart-blank-host-reentry.log`；旧包稳定对照 `/private/tmp/g108r-provenance-blank-old.log`；双代同文与异文 `/private/tmp/g108r-provenance-generations-{current,old}.log`、`/private/tmp/g108r-provenance-generation-changes-{current,old}.log`。旧包对照借主仓外部runtime依赖，仅作生产字节差分，不称隔离安装验收。正式014新增9项反例先红：空白及旧缺证5项、跨代4项，日志 `/private/tmp/g108r-blank-formal-red-final.log`、`/private/tmp/g108r-generations-formal-red.log`。

canonical污染的旧包对照 `/private/tmp/g108r-canonical-blank-before.log`、`/private/tmp/g108r-provenance-canonical-blank-old.log`明确单列；旧临时blank探针在新规则下取到最终占位，不能把其长度指标说成修复转绿。前候选595正式014为22项/21 pass/0 fail/1 integration skip，日志 `/private/tmp/g108r-core-second.log`、`/private/tmp/g108r-evidence-nine-595-final.log`、`/private/tmp/g108r-provenance-core-final.log`；这些结果没有覆盖R2-05，不能作最终验收。

R2-05新旧包对照 `/private/tmp/g108r-provenance-first-generation-{old,r1,current}.log`；真实物理Part.update canary `/private/tmp/g108r-provenance-physical-edit-canary-598.log`，原物理身份和正文回读完整。新增两项正式先红 `/private/tmp/g108r-first-capture-formal-red.log`；generation598核心Node/Bun各24项/23 pass/0 fail/1 integration skip，`/private/tmp/g108r-core-generation-bound.log`、`/private/tmp/g108r-provenance-core-{node,bun}-598.log`；独立首次捕获2074→2074，`/private/tmp/g108r-provenance-first-generation-fixed-598.log`。Trace opaque正反独立编译1/1，`/private/tmp/g108r-opaque-compile-final.log`。

R2-06旧/598对照 `/private/tmp/g108r-same-generation-edit-{before,probe}.log`，正式两项先红 `/private/tmp/g108r-same-generation-formal-red.log`。最终600的同代编辑两类均拒绝，`/private/tmp/g108r-same-generation-edit-600.log`、`/private/tmp/g108r-provenance-same-generation-fixed-600.log`；g1首次捕获2074/2074完整，`/private/tmp/g108r-provenance-first-generation-fixed-600.log`。工具早期重送只被relay裁剪的调查 `/private/tmp/g108r-tool-relay-control.log`保留，真实Opening先行正负控 `/private/tmp/g108r-engineer-tool-fixture-opening.log`，不将前者算恢复来源证据。最终正式014共26项/25 pass/0 fail/1 integration skip，Node `/private/tmp/g108r-core-600-delivery.log`，独立Bun `/private/tmp/g108r-provenance-core-bun-600.log`。007全文件63 pass/0 fail/1 integration skip，`/private/tmp/g108r-grounding-owner-final.log`；后两次为实际呈现owner重放，不冒称三次Host capture。

| 工作 | 当前状态 |
|---|---|
| 非作者初审与新旧包归因 | 完成；四个R1新增P1、原输入问题、review误判及施工/验收错误分开保留 |
| 契约与实现修正 | 完成；删除物理入口还原，来源查询/剥离只留呈现owner；当前捕获代公式由Trace拥有，不改原件格式、库包或捕获codec |
| 聚焦、实际Host及相关回归 | 完成；600核心Node/Bun各25 pass/0 fail/1 integration skip、真实Host1/1、opaque编译1/1；最终1363项0普通失败 |
| 精确包、非作者复验与交付 | 完成所列验证；2e0f精确包独立Node/Bun各12组暖/新OS冷读，三名非作者复核通过，随本原子提交正常推送master |

最终600/metadata601精确包已通过真实pack/独立默认入口，以及非作者同包Node26.6/Bun1.3.14各12场景、各12个新OS冷重放。2076文件/13,375,692字节，SHA-256 `2e0f994058540b58a1c31c2bef1dc8954ccc1b0abaf6ca4871ac2e61e2da5c30`；引用、空白四组合、旧缺证呈现拒绝、current-gen四组合和首次g1捕获前Pair保真两组合全部通过。独立npmci、只用包内wanxiangli0.6.4，全部安装字节/包摘要消费前后不变、目录已清理；`/private/tmp/g108r-package-600.log`、`/private/tmp/g108r-isolated-package-600-independent.log`。相对f047及中间59b77仅Capture、Materialization、PairProgrammingThoughtTransform、PluginTransforms四个预期dist变化，其余2072文件相同，`/private/tmp/g108r-package-delta-600-independent.log`。实际Host600独立1/1 `/private/tmp/g108r-host-600-independent.log`。

最终验收以600生产字节为准：

- 三组正式默认回归共**1363项/1312 pass/0普通fail/12 skip/39 TODO**，144个计划文件全部完成，命令exit0、监管收尾accepted=true。呈现/prefix/grounding/provider为425/405/0/4/16，Trace/WorkRecord/压缩为657/633/0/4/20，Host边界为281/274/0/4/3；日志 `/private/tmp/g108r-presentation-600.log`、`/private/tmp/g108r-trace-600.log`、`/private/tmp/g108r-host-boundary-600-final.log`。C36原失败组 `/private/tmp/g108r-host-boundary-600.log`保留，不计通过；修后重跑整组，未将单选重复相加。运行方式仍为`WXS_ACCEPT_TODO=1 TESTS_MJS_FILES="<各组编号测试逗号列表>" node requirements/verification-system/tests/run.mjs`，TODO/skip不计通过。
- 正式014新增13项均先红转绿，总26项/25 pass/0 fail/1 integration skip；原引用、空白、旧缺证呈现、同代冲突、新代首次物理捕获、当前代原文及owner冷重放分别核对。独立Node/Bun、实际Host、物理Part.update和新OS冷恢复日志见上文；物理输入不做正文还原，不凭人工回灌证明真实Host回流。
- 最终Fable `/private/tmp/g108r-build-physical-capture.log`成功，174 surfaces/839 modules；Trace opaque正反独立Fable编译1/1。静态门禁 `/private/tmp/g108r-check-600.log`、格式检查 `/private/tmp/g108r-format-600.log`通过；没有增加豁免或dotnet build。收尾文档/夹具仅刷新元数据，包仍为上述2e0f；最终新鲜度/2076文件闭包检查记录 `/private/tmp/g108r-final-600-closure.log`。
- g108r_provenance复核了同snapshot代证据、原件/呈现职责及最终Bun/同代冲突/g1首次取材；g108r_host复核了真实Host、Opening先行工具例及精确包实际消费者；g108r_evidence复核了归因、正文/全wire断言和C36协议幂等。没有把裁剪、未触达断言、同进程跨writer伪前提或合法Host元数据变化当作产品回退或验收成功。

前候选595验收证据（收尾R2-05后已被替代，不能算最终交付）：

- `node scripts/build.mjs`第二次构建成功，生产generation595，metadata596；174 surfaces / 839 modules。`/private/tmp/g108r-build-second.log`、`/private/tmp/g108r-build-metadata.log`。本轮没有使用dotnet build。输入改变导致的首轮失败已单列，未当成成功。
- 正式核心014为22项/21 pass/0 fail/1 integration skip；Node `/private/tmp/g108r-core-second.log`，非作者 `/private/tmp/g108r-provenance-core-final.log`、`/private/tmp/g108r-core-green-independent.log`。最终在原有user carrier例补注册Host canonical重入，Node单选1/1 `/private/tmp/g108r-canonical-user-final.log`；Bun1.3.14以新进程独立HOME跑最终完整014仍21 pass/0 fail/1 skip，`/private/tmp/g108r-provenance-core-bun-final.log`，临时配置已清理。该补充替换原用例验证，不在总数重复相加。
- 正式默认三组共**1359项/1308 pass/0普通fail/12 skip/39 TODO**，144个文件全部完成，进程监管收尾均accepted=true、各命令exit0。呈现/prefix/grounding/provider为421/401/0/4/16，Trace/WorkRecord/压缩为657/633/0/4/20，Host边界为281/274/0/4/3。命令仍为 `WXS_ACCEPT_TODO=1 TESTS_MJS_FILES="<对应目录编号测试的逗号列表>" node requirements/verification-system/tests/run.mjs`；日志 `/private/tmp/g108r-{presentation,trace,host-boundary}-verified.log`。TODO/skip不计通过。
- 非作者真实OpenCode1.18.29、原生read、本地provider网络、Host持久原件和退出后新进程Trace冷读1/1：`/private/tmp/g108r-host-green-independent-fixed.log`。注册Host的四种blank分层对照均通过：`/private/tmp/g108r-canonical-blank-green-layered.log`。原引用和工具completed/error完整包装字面引用仍保真，`/private/tmp/g108r-tool-quote-probe.log`。
- 非作者跨代异文physical/position在新OS Node进程重开Pair并逐字重放通过，`/private/tmp/g108r-provenance-generation-changes-process-cold.log`。另父进程写旧空白occurrence并关闭，新OS进程启动真正注册Plugin，确认自己持有旧fact后明确拒绝缺原文的呈现，hook输入与Trace/head均不变，`/private/tmp/g108r-legacy-canonical-cold-fixed.log`。此前同进程跨writer造历史的两种探针看不到新fact，不满足freshHost前提；另一次对象比较未先clone，均不算产品失败或验收成功。
- 精确主包generation596：**2076文件/13,377,127字节**，SHA-256 `59b77cfc01d640c996cbdbcdb6a394faabc3c49d4250e16baee992812f96824e`；真实pack、独立安装和默认入口通过，`/private/tmp/g108r-package-verify.log`。非作者用同一archive另行离线npmci隔离安装，Node26.6与Bun1.3.14各10场景、各10个新OS子进程冷重放：引用、空白四组合、旧缺证拒绝、physical/position同文及异文跨代全部通过。移除顶层wanxiangli，只用包内0.6.4；全部安装文件与解压包逐字节相等，前后文件及包摘要不变，目录已清理，`/private/tmp/g108r-isolated-package-independent.log`。相对f047旧包，只有Materialization和PairProgrammingThoughtTransform两个dist模块变化，其余2074文件相同，两包与对照目录收尾检查通过，`/private/tmp/g108r-package-delta-independent.log`。
- 三名非作者为g108r_provenance、g108r_host、g108r_evidence：来源/职责和真实消费者未再发现有效回退；新增9项独立9/9，旧三项共享fixture没有放宽业务断言。完整静态门禁和格式检查通过，`/private/tmp/g108r-check-first.log`、`/private/tmp/g108r-format-first.log`；收尾另核构建新鲜度与精确包闭包。没有增加静态豁免或绕过捕获资格。

新014直接证明Host/Trace/provider入口。后续消费者的依据为Companion/Blogger从canonical Trace取材、WorkRecord从Trace物化的结构核查和既有相关回归；没有新增逐项带canary的WorkRecord/compaction端到端证明。原始物理Host重建与精确已呈现carrier重入分开验证，后者反例不声称OpenCode正常请求必然复用provider wire。旧缺证历史拒绝或保留原值，不自动迁移；整张CUT-17、GAP-101/109/115/116及R12-U1的边界不变。

CUT-17-R2限定范围记DONE，GAP-108按真实物理入口/呈现owner分层证据重新闭合。旧空白缺原件、已错写事实不自动识别或迁移；完整CUT-17及其余GAP不由此关闭。没有改wanxiangli库或用户Host部署；下一包仍为CUT-02-R6-B/GAP-092，R12-U1继续暂缓。
