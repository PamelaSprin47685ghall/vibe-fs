# GAP-111：跨 invocation 的 WorkRecord 取材

输入：主仓 `53cba28de`，库 `10bbb32 / 0.6.4`；工作区干净。按[当前计划](REMAINING-REPAIRS-2026-10-16.md#gap111-next)认领 CUT-06-R1，状态只在[看板](BOARD.md#source-audit-repairs)维护。R12-U1 按用户要求继续暂缓。

首次交付为 `e7a150937`。下方施工记录保留当时结论；当前结果以[独立复审与修正](#independent-review)为准。本轮修正两个bounded Terminal去重问题：一项为e7新引入的回退，另一项在修复前已有。不能把两者统称原拆分问题。

## 原因与契约

`Materialize.fs` 的 bounded 路径采用所有相交 frame，再用全会话 RecordCoverage 决定 raw 起点。跨起点 frame 会带入旧工作；只过滤摘要仍沿用其 coverage，又会遗漏本轮原文。既有 work-record-004 TODO 已在 `c3689035c` 的真实 journal 反例中失败，本包不是 T-01 新引入的问题。

依据 work-record-002/003/004/005/007/010/014/016 和 context-compression-029：frame 作为不可分割的已确认摘要保留。只有完整落在本次工作范围内、且没有跳过本轮未表示原件的连续摘要前缀，才可进入 bounded Chronicle。遇到跨界材料后，从已采用前缀的末端读取同一 Trace 的 bounded 原件，沿用既有 WorkRecord 工具材料过滤和明确 Terminal 绑定。没有采用该 frame 就没有获得它的展示覆盖证明；durable RecordCoverage、PrefixCoverage 和历史 frame 不回写。

生产同步消费者是 SyncDelegate 的实际完成路径，异步消费者是 Fork 的实际完成及 Join。旧 Inspector 工具和角色已退役，本包不重新开放；验证内部 Engineer 同步调用及异步 fork/resume/join，不能把历史名称当成仍有效的入口。

## 施工与验收

1. 固定原 TODO，补完整内含、跨起点、跨终点、跨两端、多 frame、工具材料、正式终结与空区间的真实 journal 矩阵，同时检查旧内容隔离和当前内容完整。
2. 用现有 opaque harness 的 scoped JournalHandle 在实际 writer 中提交观察，回调返回或抛错后立即使借用失效，不释放消费者持有的 writer。同步和异步运行时仍调用生产物化器；不在 JS 复制取材公式，也不导出内部 journal 或 projection。
3. 修正 bounded 的 frame 选择及实际采用材料的覆盖，删除旧 overlap/全局 clamp 路径。现有全会话读取和 prefix 物化各自保留原契约。
4. 验证冷重开、真实消费者、后续追加及坏 blob 的拒绝；定向跑 WorkRecord/Trace/Compression/Prefix/Delegation/Horizon 与静态门禁，最终固定候选后非作者复验。
5. 更新确切测试计数、原始红绿日志、包身份、归因和剩余范围；本包不关闭 GAP-101/108/109、CUT-06 整卡或 O-01。

## 进度与证据

### 生产修正与归因

| 项目 | 归因、修正和证据 |
|---|---|
| G111-01：相交摘要越界与排除后漏原文 | 本包输入已有缺陷。`framePrefixInRange` 跳过完全早于本轮的 frame，只接纳完整内含的连续前缀；首个跨界 frame 之后不能借后续摘要跳过 raw。suffix 起点由实际采用的最后 frame 证明，不改写任何持久 coverage |
| G111-02：尾部工具使正式陈述消失 | 本包输入已有缺陷，非作者指出后补两个正式反例，原562均失败。bounded raw 起点同时保留本范围最后助手 text；Terminal 去重只看实际 raw，而非未展示的整个 workRange。无/有匹配 Terminal 的原反例均转绿 |
| 验收接缝 | JournalHandle 的 internal borrowed constructor 只创建 scoped capability，release 为 no-op；两类已有 harness 在 finally 使其失效，不暴露 AgentJournal/projection。数据仍经过实际 capture、ContextFact、fold、BlobWriter 和生产物化器。新增 squash 入口只是 plain payload 到已有正式 fact 的编码，无第二套选择算法 |
| 后续表示冻结 | 真实 squash 合并旧/本/后轮 frame 后，固定范围当前派生视图安全回到原件，冷恢复一致；表示可从 Chronicle+Recent 改为纯 Recent。已完成WorkRecord的持久冻结与实际消费仍需另验，不把当前派生视图变化当作已发布完成记录被改写的证据，也不关闭整个GAP-111/109 |

### 先红与夹具纠正

- `gap111-bounded-red-561.log`：原生产算法的004为 **14 / 6 pass / 8 fail**；跨起点、跨终点、跨两端、跨界后内含和后续追加均有实际失败。更早 `gap111-bounded-red-53cba28de.log` 有9失败，其中1条把 reasoning 的既有裸文本渲染错认成 assistant 行；只修断言后保留真正的8条红例。
- `gap111-real-consumers-red-562.log`：实际 SyncDelegate 完成和 Fork resume/Join 均混入 `PRIOR_DETAIL`，**1 pass / 2 fail / 1 TODO**。`gap111-statement-red-562.log`：已完全覆盖正文后又有工具时，无/有匹配Terminal均只剩Chronicle，**0/2**。
- 早期另起writer再Refresh的夹具出现暖冷Terminal前沿不同，`gap111-sync-fixture-diagnostic.log`。它扩大成了未建立所需因果顺序的跨writer实验，未作为本包验收依据，也未据此修改durable协议；最终改为生产实际writer提交，冷读才另起实例。该观察不证明任意跨writer捕获/终结排序已受本包承接。
- 早期fork resume错误地传入了新calling而非空调用字段，导致未生成预期prompt的等待；原 `gap111-real-consumers-red-561.log` 被中断保留。Join按既有TOML指令注释编码换行，不直接承诺wire含原始CRLF；最终验证canonical冷读保留原值，Join对应其实际注释投影。
- 原004复用终态、011 Terminal正控的frame同时覆盖caller charge和assistant，而ForRun按本轮前已有WHAT-007只允许assistant范围。分别拆为caller/current两条实际frame，继续保留Chronicle+Terminal正控；非作者确认未通过放宽实现迁就测试。
- 最初损坏夹具使用不存在的零digest，在存储层先被 `MissingPayload` 拒绝，`gap111-core-candidate-563.log` 的两条失败未触达物化器。修为两个真实已验证payload之间的错绑：selected frame拒绝，excluded crossing frame不阻止有确切原件的bounded视图。另一次Bun不支持Node的嵌套 `context.test`，`gap111-core-final-bun-563.log`；已改回顶层TODO，不改生产语义。
- 复审表述校正：squash独立探针只有固定range和真实frame/squash，没有写Terminal或完成WorkRecord冻结事实。最初把它称为“completed range”的字节冻结缺陷过强；非作者已确认校正。当前证据证明固定range的派生表示变化，完成记录的持久冻结与真实消费另留证据项，不新增一个未经证明的产品错误。

### 验收证据

生产构建 `node scripts/build.mjs`：generation **563**，174 surfaces / 838 modules，`gap111-build-candidate.log`。格式 `gap111-format-check.log`、静态 `gap111-check-candidate.log` 均exit0；未增加门禁例外。以下日志均在 `/private/tmp/`。

| 不重叠默认组 | total / pass / 普通fail / skip / TODO | 日志 |
|---|---|---|
| WorkRecord、semantic-trace、prefix-stability | 318 / 300 / 0 / 1 / 17 | `gap111-trace-record-prefix-delivery-564.log`；最终顶层TODO与011夹具已复核 |
| context-compression、delegation、participant-horizon | 598 / 549 / 0 / 6 / 43 | `gap111-consumer-compression-563.log` |
| durable-events、obligation-ledger | 283 / 247 / 0 / 27 / 9 | `gap111-durable-coverage-563.log` |
| 合计 | **1199 / 1096 / 0 / 34 / 69** | TODO/skip不计通过 |

扩大组第一次 `gap111-trace-record-prefix-563.log` 有1条011正控失败，按上方范围前提修正后转绿，保留原失败日志。核心004/010/011最终在Node、Bun均 **30 / 28 pass / 0 fail / 2 TODO**：`gap111-core-delivery-{node,bun}.log`；这是上述组的子集，不再相加。

非作者 `gap111_review` 未修改仓库或重建产物。在固定563做 source review、自有Node/Bun探针及独立新进程冷恢复，`gap111-independent-{node,bun}.log`；正式004/010当时各 **24 / 23 pass / 0 fail / 1 TODO**，`gap111-independent-requirements-{node,bun}.log`。随后只读复查004/011正控的范围拆分与新增历史冻结TODO，确认不构成产品回退。两个运行时的真实squash表示变化另记 `gap111-squash-boundary-{node,bun}.log`，不把未承接的字节冻结记为通过。

两项单独启用 integration tier：semantic-trace-005 独立 F# 消费者正反编译 **1/1**，`gap111-independent-compile.log`；obligation-ledger-004 非空 Trace/Terminal checkpoint 保留 **1/1**，`gap111-checkpoint-integration.log`。不与默认组重复相加。

文档/测试元数据刷新至564，生产行为与563相同，`gap111-build-delivery.log`。实际 `npm_config_cache=/private/tmp/p1-npm-cache node scripts/verify-package.mjs` 通过，`gap111-package-delivery.log`：**2075 members / 13,323,845 bytes**，SHA-256 **`ce6e084c2f0f6038edd5ad483bdbc62cd86e8ae0f5dd552caa3676335a495be1`**。真实 pack、完整闭包核对、隔离安装与默认入口消费均通过；库仍为原0.6.4，未改版本、锁或vendor。

同一确切tarball由非作者另在隔离目录按精确lock离线 `npm ci` 安装117依赖；删除顶层wanxiangli，实际使用包内bundled0.6.4。安装文件与tarball逐字节一致，依赖及链接均在隔离目录，没有借主仓/兄弟仓runtime。Node/Bun包外探针均exit0，覆盖borrowed-owner、跨界范围、正式陈述和独立新进程冷恢复。`gap111-package-review-{ci,integrity,isolation,node,bun,cleanup}.log`；首次直接离线install缺packument的 `gap111-package-review-install.log` 保留，不记产品失败。安装及临时Git目录已清理。

CUT-06-R1按上述限定范围完成，随 e7 原子提交正常推送master；不强推，不部署用户Host。`gap111-check-delivery.log`、`gap111-format-delivery.log`与diff检查通过。最后仅刷新文档/测试元数据并核对同一包身份，不冒称又一轮全套行为验收。

<a id="independent-review"></a>
## 独立复审与修正（CUT-06-R3）

复审输入为干净主仓 `e7a150937`、未改动的库 `10bbb32 / 0.6.4`。`g111r_ranges`、`g111r_consumers`、`g111r_contracts` 均未参与 e7 实现；三者分别检查范围与 coverage、真实消费者与借用寿命、契约与完成记录持久化。它们未编辑共享仓或重建共享产物；root 串行修正、构建和发布。R12-U1 未重新调查。

### 问题与归因

| 项目 | 结论与处置 |
|---|---|
| G111-01/02，原有范围越界、漏原文和尾部工具遮蔽正式陈述 | 原修正继续成立；frame 连续性由 Blogger 的前游标相等、严格前进和 squash 只替换最旧前缀保证。既有负控与独立分区、冷恢复、消费者对照继续通过 |
| G111-R3-01，P2：同源 Terminal 重新插入已摘要正文 | **e7 新引入，前轮 review 漏检，本轮已修复。** 同一助手 turn 的 reasoning/text 前半已进入 Chronicle，而正式 text 仍在 raw gap 时，gap-only 比对误将完整 Terminal 判为未捕获；结果正文重复、已摘要前半重新进入 Recent。原独立 Node/Bun 反例均失败，新增18条正式矩阵在566为11pass/7fail。不能只回退到完整 workRange 判重，否则会重开“工具后只剩空白却不补 Terminal”的缺口 |
| G111-R3-02，P2：完整raw配仅formal Terminal仍重复正文 | **输入已有，本轮包外复审发现并修正。** 同一自然turn的reasoning和text都在raw，Terminal仅为末条text，旧整turn聚合不相等而重复追加。至少53c源码已有该公式，无frame也可触发，不能归为e7或本轮修正新引入；未独立重建53实跑。170b中间包Node/Bun均红，另8条正式矩阵在568为4pass/4fail |
| 当前修正 | 保留既有 boundedGapStart 的最后助手正文。先以实际gap的完整自然turn或该turn最后text的整body精确判重；不能以任意较早text代替末条正式正文。若仍不等，只有最新自然turn确有已验证、非空白原件进入gap，才以本范围完整turn精确比对Terminal。gap是连续suffix，因此此证据也包含后续自然材料；空白、user或media不能冒充该证据。不同Terminal仍完整补出，不裁切/包含匹配Terminal，不回写coverage；全会话去重仅提取原有选择helper，语义不变，本包不扩称全会话取材已整体闭合 |
| G111-R2，已完成范围的历史重物化 | **已确认的既有 WHAT-004 缺口，继续由 CUT-06-R2 处理。** 本轮真实 Fork 已写 Terminal/HandleCompleted 后，后续 squash 仍能改变当前投影重物化的字符串。这比上一轮未完成事实的探针更强，不能再仅称“冻结待补证”。同时，已保存完成 blob、实际同进程 Join 及冷恢复消费保持原表示，不能称已发布完成记录被改写 |
| 旧 Host 消息新增部件的观察 | 同物理 part 修订被拒绝且 Trace 不变；已终结旧消息在新 invocation 内首次出现全新 part，则按首次确认 cursor 进入新范围。先前暂称“旧内容混入”过强：它不是此前已确认原件，WHAT-002 也不以 transcript 下标定义范围。正常 Host 是否会在终结后这样增长尚无证据；捕获/起点路径在53→e7未变，无frame时原算法读取同一range，静态归因不支持 e7 新回退。保留观察，不为此修改 Trace 保真或添加正文过滤 |

### AGENTS.md 与验收边界

- 只手工修改私有物化逻辑、004/010编号测试及本包文档；无接口、fsproj、版本、lock 或 vendor 变更。Trace 仍保管原件，Blogger 仍保管 frame/coverage，WorkRecord 只组合已验证材料，没有第二份持久 owner。
- 使用 `node scripts/build.mjs`，未使用 `dotnet build`。567 候选行为虽绿，静态门禁仍拒绝两处嵌套控制分支；root 将可见 suffix 证明独立成一步、用 Option 读取终态后，568构建及静态门禁通过。未增加门禁例外。
- 首轮受限沙箱中的 Git 工具身份探测失败，trace组则因未启用 TODO override 返回非零；原日志 `gap111r-*-baseline.log` 保留。相同源在授权工具环境及 `WXS_ACCEPT_TODO=1` 下重跑，三个基线组1199项0普通失败。override只允许带未完成证据结束，不把TODO/skip记为通过。
- 新010夹具最初给 `withJournal` 的 Task 回调返回了同步对象，两项失败。按既有契约补 `async` 后转绿，未让产品API兼容错误夹具。`gap111r-core-{node,bun}-568.log`及非作者首次日志保留；004一处未写完成事实的旧标题改为 earlier bounded，避免夸大证明。

### 最终候选与证据

生产源固定于 **generation570**：174 surfaces / 838 modules，`gap111r-build-formal.log`；测试元数据571，`gap111r-build-package-ready.log`。568/569及170b包为第一个修正候选，其通过不能代替包外新反例的修正。以下日志均在 `/private/tmp/`。

| 不重叠默认组 | total / pass / 普通fail / skip / TODO | 日志 |
|---|---|---|
| WorkRecord、semantic-trace、prefix-stability | 346 / 328 / 0 / 1 / 17 | `gap111r-trace-record-prefix-final-571.log` |
| context-compression、delegation、participant-horizon | 598 / 549 / 0 / 6 / 43 | `gap111r-consumer-compression-final-571.log` |
| durable-events、obligation-ledger | 283 / 247 / 0 / 27 / 9 | `gap111r-durable-coverage-final-571.log` |
| 合计 | **1227 / 1124 / 0 / 34 / 69** | TODO/skip不计通过 |

核心004/010/011在Node/Bun各 **58 / 56 pass / 0 fail / 2 TODO**，root日志 `gap111r-core-{node,bun}-570.log`，非作者日志 `gap111r-core-independent-{node,bun}-final-570.log`。新增26条Terminal矩阵：18条覆盖部分reasoning/text、完整同源/仅suffix/不同Terminal、尾工具、空白suffix及正式陈述；另8条覆盖完整raw有/无跨界frame、末条formal去重及较早正文同字不得误吞的新终态。两个红矩阵日志为 `gap111r-terminal-red-566.log`、`gap111r-formal-only-red-568.log`。两条010走真实Fork完成→后续squash→同进程Join或冷journal消费；当前派生表示确已变化只是实验前提，不算WHAT-004已满足。

额外 host-boundary-021 **20/20**，`gap111r-host-boundary-021-final-570.log`。独立Fable消费者正反编译、非空Trace/Terminal checkpoint两个integration各 **1/1**，`gap111r-independent-compile-final-570.log`、`gap111r-checkpoint-integration-final-570.log`；后两项不与默认组重复相加。格式与静态门禁日志为 `gap111r-format-check-formal.log`、`gap111r-check-formal.log`。

非作者在570再次用自有探针验证每runtime **32种frame分区、224个暖视图、224个独立新进程冷视图**，逐项检查范围内自然材料完整、范围外及工具协议不存在、暖冷字节相同；原Terminal重复和空白fallback探针均转绿。日志 `gap111r-ranges-independent-{node,bun}-final-570.log`、`gap111r-terminal-{suffix,empty}-independent-{node,bun}-final-570.log`。借用寿命、真实双轮Sync/Fork及caller charge/后续澄清边界曾在567的Node/Bun复验通过，`g111r-borrow-{node,bun}-567.log`、`g111r-consumers-{node,bun}-user-boundary-567.log`；570最终核心真实消费者也已复验，包外另用最终确切包重测。

完成记录的独立探针 `gap111r-contracts-freeze-final-{node,bun}.log` 通过：Fork将确切WorkRecord存入v2完成blob，Join经digest校验后解码并CAS消费，后续squash不改blob。新进程读取同一ref/digest/字节；真实Join按本进程归属契约排除旧handle且不消费，随后另开journal的持久consume返回原表示。**冷consume不等于真实Join验收。** 对应源码为 `Fork/Host/RunLifecycle.fs`、`Handle/CompletionCodec.fs`、`Handle/JoinDrain.fs` 与 `Fork/Host/Join.fs`。

精确主包验证571通过，`gap111r-package-final-571.log`：**2075 members / 13,326,481 bytes**，SHA-256 **`eeaa8b243ed2da6e292bd87d549ed5a810d3e75820c39310dc108df4e7f4d92f`**。真实pack、闭包校验、隔离安装与默认入口通过；库仍为原0.6.4。170b中间包的合法formal-only反例保留为 `g111r-package-review-{node,bun}-suffix-terminal-failure.log`，不以改成whole-turn输入替代它。

同一eeaa包的非作者隔离精确离线安装117依赖、前后逐字核对全部2075成员及7条目录内symlink通过，删除顶层wanxiangli后以ESM实际解析包内0.6.4。没有主仓runtime/support引入，工具schema为手工plain输入数据。Node/Bun各14材料组、真实SyncDelegate/Fork双轮均通过，每runtime另启3个子进程，分别对照14组材料、Sync及Fork冷读，全部逐字相等；formal-only原反例在同输入下转绿。日志 `g111r-package-review-final-{node,bun,ci,integrity-before,integrity-after,cleanup}.log`。最初用CJS解析import-only子路径失败属于复审方法误判，按真实ESM边界校正，未改包来迁就探针。临时安装及Git/journal目录均清理。

CUT-06-R3限定范围完成，两个问题的归因分别保留，随本原子提交正常推送master。最终只刷新文档元数据并核对同一包身份；不将其冒称又一轮行为测试。库未改，未部署用户Host，GAP-111与R2的剩余义务不关闭。

<a id="remaining-boundaries"></a>
## 剩余边界

- 本包只关闭跨界摘要越界、排除后漏原文、正式陈述被尾部工具遮蔽的已确认反例。完整且连续的摘要仍可复用；跨界后原件来自同一Trace，EventStore继续是唯一持久载体，durable RecordCoverage/PrefixCoverage不回写。
- GAP-111保持PARTIAL。本轮真实完成后squash已确认：同一完成range按当前投影重物化的表示仍可改变，WHAT-004这一项尚未满足，属于既有缺口；完成blob、实际Join和已验证冷消费保持冻结。CUT-06-R2按[后续计划](REMAINING-REPAIRS-2026-10-16.md#gap111-history)盘点其他消费者的完成表示与读取，再修历史查询的冻结义务，不为此向Trace添加Blogger状态或创建任意range的第二份写入权威。
- 实际同步Engineer和Fork resume/Join已验证；退役Inspector不开放，真实provider、完整Opening/Fission及所有消费者的生命周期证明仍归GAP-101/109。scope借用是已登记验收边界，不是新的产品业务入口或持久owner。
- 独立writer未建立因果顺序的捕获/终结实验保留原日志，不据此宣称跨writer排序已验收。旧坏历史也不自动改写。R12-U1继续按用户要求暂缓，本包未调查物理Unknown、未部署用户Host。
