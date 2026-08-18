# WHAT —— durable-events（唯一 normative 合同）

条款前缀 `DURABLE-EVENTS-`。每条的落点测试见 `HOW.md`。
来源：历史五层 persist 条款（PERSIST-001..010）、
历史 change（storage）（§1–§48）、历史 COVERAGE persist 小节。

## Semantic boundary（本包 JS contract）

语义测试只观察 JS-native values；它们不导入 Fable emitted internals 或测试侧
interop facade。持久资源由 opaque capability 句柄承载，并必须显式 `dispose`：

| owner surface | laws | production source | representation |
|---|---|---|---|
| `Persistence/EventStore/CodecSurface.js` | canonical bytes, decode-only compatibility, identity collision | `src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs` | plain event/result objects and strings |
| `Persistence/EventStore/MergeSurface.js` | writer-stream k-way order and identity union | `src/Wanxiangshu/Persistence/EventStore/EventKWayMerge.fs` | arrays of plain events/results |
| `Persistence/EventStore/Surface.js` | local append/read/heads and resource lifecycle | `src/Wanxiangshu/Persistence/EventStore/Store.fs`, `ProcessEventLog.fs` | opaque `EventStoreHandle`; plain receipts/events |
| `Persistence/Journal/CodecSurface.js`, `FactCodecSurface.js` | journal envelope/fact bytes and decode-only compatibility | `src/Wanxiangshu/Persistence/Journal/EventStoreJournalCodec.fs`, `FactCodec.fs` | plain envelope/fact descriptors and byte results |
| `Persistence/Journal/Surface.js` | journal boot, append, payload resource and projection summary | `src/Wanxiangshu/Persistence/Journal/AgentJournal.fs`, `EventStoreJournalWriter.fs` | opaque `JournalHandle`; plain projection/receipt objects |

The F# production internals may retain domain types; only these owner surfaces translate them at
JS boundaries. A semantic law must name one owner, its source, representation, and proof anchor in
`HOW.md`; a monolithic test helper that imports multiple domain owners is not a contract surface.

## DURABLE-EVENTS-001 —— Event 是唯一 durable truth；append-only

**规范陈述**：任何动态业务状态只以不可变 event 表达；修改 = append 新 event，删除 =
append tombstone/retirement event；committed event 永远不可修改、覆盖、删除、原地升级
或重新解释。Projection 不是第二真相源：禁止先改投影、以后补 event。

**含义/动机**：历史只增长、不回写。错误事实通过新事实纠正，否则重放无法回到同一局面。
**边界**：各 event 的**业务语义**归各 domain owner；本命题只钉「事实如何被存储与演化」。
**证据**：→ HOW.md 001。

## DURABLE-EVENTS-002 —— EventEnvelope 无版本；additive vocabulary

**规范陈述**：每个 durable event 是版本无关的 `EventEnvelope`（`event_id`、`stream_id`、
`event_type`、`parents`、`payload`、`payload_refs`）。禁止 envelope/store 携带
`schemaVersion`/`storageVersion`/`journalVersion`/`formatVersion`/`generationVersion`。
同一 `event_type` 的 payload shape（字段名、含义、必填性）一经 committed 即冻结；新语义
必须新 `event_type`（additive vocabulary）。unknown authoritative `event_type` 必须
fail closed（见 007）。

**含义/动机**：版本不是领域事实；消灭的是 storage-version compatibility，不是
historical-event compatibility。已 committed 的 `event_type` 语义冻结，旧 decoder 永久有效。
**边界**：领域事实的语义解释不归本包；「哪个 event_type 是合法的」由
`AuthoritativeEventTypes`（store 层 vocabulary）+ 各 domain 词汇表共同决定。
**证据**：→ HOW.md 002。

## DURABLE-EVENTS-003 —— canonical JSON 是 identity 协议

**规范陈述**：canonical JSON：UTF-8、无 BOM、恰好一个 LF 结尾；object key 按 Unicode
codepoint 升序（递归）；`parents` / `payload_refs` 先去重再按 canonical 文本序排序。
同 `event_id` + 不同 canonical bytes → identity collision，fail closed；
同 id + 同 bytes → 去重为一个 event。

**含义/动机**：canonicalization 不是实现细节，是 identity 协议——若 `[A,B]` vs `[B,A]`、
key 顺序、数字格式、Unicode escaping 不冻结，重放身份就会漂移。
**边界**：merge 层如何用 identity 做 set-union 见 `durable-convergence`。
**证据**：→ HOW.md 003。

## DURABLE-EVENTS-004 —— local append 的提交原语 = 完整 NDJSON 行

**规范陈述**：`Append`/`Publish` 的本地 durable witness 是当前 process writer 文件末尾出现
完整 canonical `JSON+LF` 行；成功返回前必须完成该行写入，且必须已经**同步释放**与独立 Git hook
共享的 physical store gate：`Append`/`WritePayload` 的 Task 不得在 lock release 尚未完成、heartbeat/
retry handle 仍存活时提前完成。运行时 append **不得**创建 Git blob、Git tree、Git ref 或执行 Git CAS，
也不得因为历史长度重写既有 bytes。半行、截断行、原地改写既有 canonical 行均非法。

**含义/动机**：本地事件真相就是裸 append-only 文件；Git 是同步编码，不是在线数据库。
一次事实提交的物理成本只与新事实 bytes 有关，不再与 Git object/tree/index 数量有关。
**边界**：remote Git 操作触发的 blobification / remote ref publish 见 018 与 `durable-convergence`。
**证据**：→ HOW.md 004。

## DURABLE-EVENTS-005 —— Process = single writer；一个进程一个永久增长文件

**规范陈述**：每个 EventStore process instance 创建一个全局唯一 `WriterId`，并且只追加
`.git/wanxiang/events/<WriterId>.ndjson`。该文件**不按大小、event 数或时间切片**：就该多大多大。
进程结束后该 writer 文件永久封存；新进程必须创建新的 WriterId，不接管旧 writer 文件。
业务 `stream_id`、machine identity、role/agent identity 都不得参与物理 writer ownership。

**含义/动机**：single-writer append 本身已经消除了本地多进程写冲突；机器只是 transport
位置，不是事件模型的一层。多进程与多机都只是若干互不共享 WriterId 的有序输入流。
**边界**：这些 writer streams 如何汇合见 `durable-convergence` k-way merge。
**证据**：→ HOW.md 005。

## DURABLE-EVENTS-006 —— commit outcome 只由本地事实存在性判定

**规范陈述**：提交结局的 durable witness 是 canonical EventId 是否已经以相同 canonical bytes
存在于本地 writer/history truth 中；不得用 Git ref、内存猜测、模型重试或进程退出码代替。
同 EventId + 不同 bytes → IdentityCollision；同 EventId + 同 bytes → 已提交/幂等成功。物理 append
真正开始后失败才可表达为 `CommitUnknown`；writer 已 poisoned / closing / disposed 时拒绝的新调用从未
到达 append boundary，必须表达为 typed **known-not-attempted**，不得伪装成「结局未知」。writer 的
async release 必须先关闭新准入，再 drain release 前已准入的 serial append prefix，之后才进入 disposed。

**含义/动机**：remote 是否同步成功与“本地事实有没有发生”是两件事。把 Git ref 当 commit
witness 会把网络/remote 可用性重新塞回本地 critical path。
**边界**：remote publication failure 归同步层；外部效果的 outcome-unknown policy 归 `effect-accounting`。
**证据**：→ HOW.md 006。

## DURABLE-EVENTS-007 —— StorageInvalid 全局 fail closed

**规范陈述**：以下任一校验失败 → 拒绝以该 snapshot 构建投影或启动依赖它的 runtime 路径，
进入显式恢复/人工处置：坏 JSON、非 canonical、identity collision、缺 parent/成环、
payload 缺失或 hash 失配、unknown authoritative `event_type`、必填字段错误、
Append/Publish CAS retry 耗尽且 EventId 仍不在 store。**禁止**跳过坏 event 继续 fold。

**含义/动机**：第一个不可能的事件即停——跳过中间坏对象继续，后续事实就建在错基上。
**边界**：合法并发 fork 不是 StorageInvalid（见 008 与 `durable-convergence`）。
**证据**：→ HOW.md 007。

## DURABLE-EVENTS-008 —— 并发 fork 不升级为全局 corruption

**规范陈述**：`DomainConflict`（合法并发 fork）不是 `StorageInvalid`：history 保留全部
competing facts，绝不因自然 fork 把 store 永久打成不可恢复。禁止把 DomainConflict
升级为全局 corruption；禁止「非法 fork → fail closed」的 Storage 层解释。

**含义/动机**：append-only union 必然能产生物理合法 fork；它与「全局不可恢复」必须正交。
冲突如何表达与裁决是 `durable-convergence` 的正向律，本命题只钉「不得混淆两类错误」。
**边界**：DomainConflict 的确定性表达、resolution 收敛 → `durable-convergence`。
**证据**：→ HOW.md 008。

## DURABLE-EVENTS-009 —— 无 schema/store generation；所有旧物理布局 shock cutover

**规范陈述**：Store 不维护 schema/store/migration generation。旧 Journal NDJSON、RuntimePath
`blobs/`、Student QA 私有文件、feature-owned ref，以及曾发布过的 one-event-per-blob
`events/<hex>/<EventId>.jsonl`、`logs/<ReplicaId>/<segment>.ndjson + index/` 等 Git roots，全部
**完全 leave-unread**：runtime/sync 不判旧 shape、不枚举旧 body、不 reset 旧 root、不迁移、不双读。
禁止 dual-write、legacy event importer、projection-equivalence migrator、fallback-to-old-store shim。

**含义/动机**：这是明确的数据休克切换：允许丢弃旧 durable history，换取零永久兼容路径、
零旧布局启动扫描。旧 Git objects 是否继续存在只由普通 Git object reachability/GC 决定，新代码不认识它们。
**边界**：只有新 `.git/wanxiang/events|payloads` 与新 remote `writers/`/`payloads/` snapshot 属当前协议。
**证据**：→ HOW.md 009。

## DURABLE-EVENTS-010 —— 单一 universal durable substrate = `.git` 内本地事件文件

**规范陈述**：动态 durable event truth 的运行时物理介质只能是 `.git/wanxiang/events/*.ndjson`
与它们引用的 `.git/wanxiang/payloads/*`；所有 `EventEnvelope`——AgentJournal、Job、Casebook、
Strength、JsTransaction 或未来 domain——都进入这套 universal writer files。feature-owned
journal/blob/store/ref、按 domain 拆 backend 非法。业务 `stream_id` 不决定物理文件；WriterId 决定。

**含义/动机**：用户 working tree 看不见运行态证据，同时运行时仍只是普通 append-only 文件；
不用 Git object graph 承担数据库职责。
**边界**：静态 repository content（resources/docs/Change）仍走普通 Git；remote 编码见 011/018。
**证据**：→ HOW.md 010。

## DURABLE-EVENTS-011 —— Git blob 只存在于 remote sync 边界；一 writer 文件 = 一 blob

**规范陈述**：Git object database 不是运行时 event store。仅当用户自己的 Git 进程执行 remote
操作并进入 Wanxiangshu 安装的 Git hook 时，当前每个完整本地 writer NDJSON 文件才按其**全部 bytes**编码为
**恰好一个 Git blob**；不得切 chunk/segment、不得维护 EventId index tree、不得设计 delta protocol。
Wanxiangshu/OpenCode 主进程不主动发起 fetch/pull/push，也不是 sync 的运行宿主。
下一次 sync 同一 writer 文件增长后，可得到新的 blob OID；旧 blob 只是不可达传输产物。

**含义/动机**：本地文件是身份稳定的真相，blob OID 只是某次同步快照的内容寻址编码。
Wanxiangshu 不参与 Git pack/delta 优化，也不让它反向污染 append hot path。
**边界**：remote root 如何列出 writer→blob 与双方替换见 `durable-convergence`。
**证据**：→ HOW.md 011。

## DURABLE-EVENTS-012 —— PayloadRef 与本地 payload closure

**规范陈述**：大正文先以 content digest 形成 opaque `PayloadRef`，bytes 保存在
`.git/wanxiang/payloads/<PayloadRef>`；event 成功 append 前，所有新增 payload refs 必须已经存在且
bytes/digest 一致。Integrator 可见的当前 truth 必须满足全部 committed events 的 payload closure；
dangling ref → StorageInvalid。remote sync 时 payload 文件才编码为 Git blob。Domain 不得操作 Git OID。

**含义/动机**：payload 与 event 一样先是本地 durable truth，Git 只是同步载体；同时保持大正文
内容寻址与去重，不把正文塞回 NDJSON。Journal fact 的 `payload_refs` 由 fact 自身携带的 blob 字段
唯一派生（`BlobRef` = `blobs/<sha256>`、`BlobDigest` = `<sha256>`，映射到同一 `PayloadRef`）；
只有真实 lowercase-sha256 content-address 才是 payload reference，非 content-address 的占位值不是
payload dependency，不进入 closure。
**边界**：digest 算法与 remote tree layout 是 HOW；业务只见 opaque PayloadRef。
**证据**：→ HOW.md 012。

## DURABLE-EVENTS-013 —— 查询只读正规 Integrator 的 Current；先 commit 后 integrate

**规范陈述**：任何当前状态查询不得扫描/过滤/手动 fold 历史，只能读取唯一 canonical Integrator
维护的 `Current` 积分态。`Current` 不是第二真相源：禁止先改 Current 再补 event；必须先完成本地
canonical 行 append/payload closure，再把同一 EventEnvelope 交给 Integrator。进程重启时 Current
只能由 Integrator 对历史执行同一套 integration rules 重建。

**含义/动机**：历史只有一个解释权，避免 Journal/Strength/Casebook/JsTransaction 各写一套
“load history → project”的隐性第二积分器。
**边界**：Integrator 的注册模型见 019；各业务状态字段意义归对应 domain owner。Terminal
completion proof 只能由 ChildRecovery/Host lifecycle owner 产生；Persistence Journal surface
不接受任意字符串来伪造 terminal evidence。
**证据**：→ HOW.md 013。

## DURABLE-EVENTS-014 —— k-way 输入顺序 + 确定性积分

**规范陈述**：每个 WriterId 文件内部按 append 顺序有序；多个 writer history 的统一输入顺序由
既有 deterministic k-way merge 产生。Integrator 必须按该 canonical order 一次处理每个 EventEnvelope；
相同 writer streams 集合必须得到相同 Current。same EventId+same bytes 去重；same EventId+different
bytes fail closed。业务模块不得自行重排历史。

**含义/动机**：单机多进程和多机分布式没有两套算法——都是若干有序 writer streams 的 k-way merge。
**边界**：k-way merge 的代数/transport 收敛归 `durable-convergence`；业务积分只消费其输出。
**证据**：→ HOW.md 014。

## DURABLE-EVENTS-015 —— business fold 不变量 owner（PERSIST-010）

**规范陈述**：business fold 对以下事实的不变量**不满足任一条 → 当前 fact semantic reject + durable cut-tail reset**：
`OpeningPromptCaptured`（每 lifecycle 幂等、不可覆盖）、`XTracePartAppended`（严格顺序
append-only、Cursor 单调）、`BlogObservationCommitted`（PreviousIngestCursor=当前、Next>Previous、
CoverableTurnCutoff 单调、TextDigest=blob、attempt Completed 且 terminal valid）、
`TerminalOutputCaptured`（幂等不可覆盖）、`BlogObservationsSquashed`（FrameEpoch+1、不改
Ingest/Coverage）、`PrefixRebaseCommitted`（Epoch+1、candidate digest 再验证、Y bundle
PrefixCoverage-complete-turn）、`ContextReanchored`（Epoch+1、同一消息 id 只接受一次）。

**含义/动机**：本命题是 fold 的**完整性机制** owner：任何事实不满足其不变量时，该次调用失败；Integrator 保留该 rule 的 last-good Current，立即持久化 `ProjectionCutTail` reset，writer/runtime 不被 poison，后续同一功能可重新尝试。不产生任何 writer 不可能产生的部分重放状态，也不隐藏坏 fact。
**边界**：各 fact 的业务语义（XTrace/coverage/epoch 的意义）归
`semantic-trace`/`work-record`/`context-compression`/`prefix-stability`/`obligation-ledger`
等 domain owner；本包只拥有「不满足即拒绝」这一条红线。
**证据**：→ HOW.md 015。

## DURABLE-EVENTS-016 —— 所有权红线：Git 物理概念不外泄

**规范陈述**：`GitObjectId`/`RootOid`/`StoreSnapshot`/`TreeEntry`/`IGitRawStore` 等 remote-sync
物理概念只属于 Persist/Git infrastructure；`refs/wanxiang/store` 只允许出现在该 infrastructure。Domain 层
不得 `open Infrastructure` 或引用这些类型；Domain 只见 `EventEnvelope` 与 opaque
`PayloadRef`。

**含义/动机**：把「事实的语义」与「事实的物理存放」隔离；领域语义不随存储机制漂移。
**边界**：这条红线的静态门禁与 fixture 见 PROOF 016。
**证据**：→ HOW.md 016。

## DURABLE-EVENTS-017 —— local append 复杂度不得依赖 history / Git

**规范陈述**：追加一个新 EventEnvelope 时，运行时 Git object/tree/ref 操作数必须为 **0**；不得读取或
重写任何既有 writer file bytes，新增写入量只与该 event canonical bytes（及显式新 payload bytes）相关。
EventId 分布、历史 event 数、writer 文件当前大小都不得改变 append 所需 Git work，因为 append 路径没有 Git work。

**含义/动机**：这是对本次性能根因的结构性封口；不能再用 index/tree 优化去修一个本不该存在的在线 Git 数据库。
**边界**：OS flush/fsync policy 是 HOW；remote sync 成本不属于 local append latency。
**证据**：→ HOW.md 017。

## DURABLE-EVENTS-018 —— remote 操作才同步；同步替换 local + remote truth snapshot

**规范陈述**：Wanxiangshu 不运行 timer/background uploader/event-count sync，也不从产品进程主动调用
fetch/pull/push。Git hook + remote store refspec 的 install/refresh 属于 **durability activation**，不得发生在 OpenCode
等待 plugin init 返回的 Load Phase；第一次真正启用该 workspace 的 durable capability 时才按需 ensure。之后只有用户自己的
Git remote 操作触发独立 hook 进程同步 EventStore。hook 必须在 OpenCode/Wanxiangshu 完全未运行时仍可独立读取
本地 writer files 与远端 writer blobs，按 `durable-convergence` 的 k-way merge/identity law 得到统一 history，
直接 materialize/replace 本地 writer truth 与 remote snapshot；不设计 delta/chunk/增量 object protocol。

**含义/动机**：同步是用户 Git 操作的副作用，不是 Wanxiangshu runtime 的第二个生命周期。`git push` 可以发生在
OpenCode 已退出以后，仍必须同步成功。全量文件很大也先保持 KISS；优化必须由真实 profiling 重新证明需要。
**边界**：remote ref/lease/transport failures 归 `durable-convergence`；本命题只钉 trigger 与物理粒度。
**证据**：→ HOW.md 018。

## DURABLE-EVENTS-019 —— 唯一 canonical Integrator；业务只注册 integration oracle

**规范陈述**：生产代码中只有一个 canonical F# CE Integrator 可以把历史 writer streams **解释/积分为 Current**；
它拥有 boot/recovery 的 history iteration、共享 `EventKWayMerge` 输入与 integration frontier。每个业务模块只能向该
Integrator **注册**单 EventEnvelope 的 integration oracle/rule；业务模块不得获得 history-reader capability，不得自行
`loadEvents` / `scan history` / `fold list` /“从 EventStore 重建 projection”。启动 replay 与在线 append 必须调用同一个
CE program、同一组注册规则。独立 remote-sync hook 可以为**纯物理 union/identity validation**读取 writer streams 并调用
同一个 `EventKWayMerge`，但它不得产生/修改任何业务 Current。

**含义/动机**：event sourcing 只有一个解释器；模块拥有规则，不拥有重放循环。这样 Current 的积分逻辑
不会因恢复/在线/feature helper 分裂成多个状态机或手写 fold。
**边界**：业务 rule 内部可维护自己被分配的 Current 槽位，但 orchestration/registration/history iteration
只属于 Integrator；实现必须使用 F# CE DSL，不引入事件状态机。
**证据**：→ HOW.md 019。

## DURABLE-EVENTS-020 —— plugin load 只验证物理可读性；业务 replay/RuntimeStarted 延迟到 activation

**规范陈述**：OpenCode plugin Load Phase 不得因为 EventStore 中某个业务 projection 无法解释而执行恢复、修复或写入新事实。Load Phase 最多验证 writer/payload 的物理结构可读性；canonical business integration、Journal projection acquisition 与 `RuntimeStarted` watermark 只能在第一次实际需要 durable semantics 时激活。未发生任何业务消费的 plugin 实例退出时，不得仅因“被加载过”而新增 RuntimeStarted 或其它业务事实。

**含义/动机**：durable history 是业务输入，不是 plugin constructor 的控制流。把 replay/RuntimeStarted 放在 constructor 会让历史语义问题升级为 Host 启动故障，也会让失败启动消耗 recovery budget。

**边界**：结构损坏（无法解析 canonical envelope、缺失必需 payload）属于物理 store unreadable，可拒绝该 workspace durable capability；业务规则冲突/unknown domain state 不得升级成 plugin-load failure，由 021 的 self-limited cut-tail 语义承接。

**证据**：→ HOW.md 020。

## DURABLE-EVENTS-021 —— semantic failure 仍写 durable cut-tail，但**当前进程必须 fatal**

**规范陈述**：registered business integration rule 对某个 EventEnvelope 返回语义错误时，Storage 不得抹掉已结构合法的坏 fact。Integrator 必须按原时序：① durable 保留坏 fact；② 该 rule 进入 faulted tail，Current 保持 last-good；③ 由该业务 rule 根据当前事实推断最小 reset patch；④ 在同一次 live append 中紧随坏 fact写入 first-class `ProjectionCutTail(rule, failed_event_id, reason, reset)`；⑤ replay 严格按 canonical 顺序先看到坏 fact，再看到 cut/reset，再继续后续 fact。`ProjectionCutTail` 与普通 writer fact 一样参与 remote sync。

但 durable 可恢复 **不等于当前进程仍可信**。live append 一旦收到“自己的 EventId 被 cut”的 typed `FactRejected` receipt，journal append boundary 必须在返回调用方之前触发 process-level fatal；禁止把 semantic cut 转成普通 tool consequence、`Result.Error` 后继续接受新 prompt/nudge/effect。原因是产生坏 fact 的同一调用可能已经改变 process-local ownership、single-flight cache、pending task 或 Host session 状态，cut-tail 只能修 durable projection，无法回滚这些内存/物理副作用。

测试环境可屏蔽物理 kill 以检查 typed receipt；生产进程必须退出。**下一次进程** replay 已 durable 的 bad fact + cut/reset 后可以从 reset Current 继续；这仍不是自动重跑旧 tool。若业务 rule 无法现场推断 reset patch，允许 canonical Integrator 做一次 full-log replay 后再次推断；整个进程全局最多一次，但一旦 live append 最终产生 cut receipt，仍 fatal 当前进程。

**含义/动机**：坏语义是历史事实，不是 storage corruption，因此 cut/reset 仍必须 durable；同时它也是 Wanxiangshu 自身 invariant break，不能作为“可继续运行的业务失败”。durable recovery 与 process safety 是两个不同维度。

**边界**：malformed canonical bytes、identity collision、missing parent/payload、unknown authoritative event type 仍属 DURABLE-EVENTS-007 的 StorageInvalid；这些不是 semantic cut。

**证据**：→ HOW.md 021。
