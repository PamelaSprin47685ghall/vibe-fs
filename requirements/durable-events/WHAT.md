# WHAT —— durable-events（唯一 normative 合同）

条款前缀 `DURABLE-EVENTS-`。每条的落点测试见 `PROOF.md`。
来源：`archive/docs/{why,what,shape,how,proof}/persist.md`（PERSIST-001..010）、
`archive/changes/completed/storage.md`（§1–§48）、`requirements-design/COVERAGE.md` persist 小节。

## DURABLE-EVENTS-001 —— Event 是唯一 durable truth；append-only

**规范陈述**：任何动态业务状态只以不可变 event 表达；修改 = append 新 event，删除 =
append tombstone/retirement event；committed event 永远不可修改、覆盖、删除、原地升级
或重新解释。Projection 不是第二真相源：禁止先改投影、以后补 event。

**含义/动机**：历史只增长、不回写。错误事实通过新事实纠正，否则重放无法回到同一局面。
**边界**：各 event 的**业务语义**归各 domain owner；本命题只钉「事实如何被存储与演化」。
**证据**：→ PROOF.md 001。

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
**证据**：→ PROOF.md 002。

## DURABLE-EVENTS-003 —— canonical JSON 是 identity 协议

**规范陈述**：canonical JSON：UTF-8、无 BOM、恰好一个 LF 结尾；object key 按 Unicode
codepoint 升序（递归）；`parents` / `payload_refs` 先去重再按 canonical 文本序排序。
同 `event_id` + 不同 canonical bytes → identity collision，fail closed；
同 id + 同 bytes → 去重为一个 event。

**含义/动机**：canonicalization 不是实现细节，是 identity 协议——若 `[A,B]` vs `[B,A]`、
key 顺序、数字格式、Unicode escaping 不冻结，重放身份就会漂移。
**边界**：merge 层如何用 identity 做 set-union 见 `durable-convergence`。
**证据**：→ PROOF.md 003。

## DURABLE-EVENTS-004 —— CAS 是唯一提交原语；无部分写入

**规范陈述**：`Append`/`Publish` 以 canonical ref 的 CAS 为唯一提交原语：
`CAS(refs/wanxiang/store, expected = Absent | R0, new = R1)`。成功 → 新 `StoreSnapshot`
（Committed）。不存在「部分写入」的权威历史：one event = one immutable blob；半条 NDJSON
不得进入 canonical root。禁止独立 `CreateRef` / 第二套首次 bootstrap 协议。

**含义/动机**：物理 authority 的原子性是「提交」的唯一定义；多进程并发 append 由 CAS
裁决，不需要 leader/锁。
**边界**：CAS 冲突后的重试语义见 005。
**证据**：→ PROOF.md 004。

## DURABLE-EVENTS-005 —— CAS 冲突：先查 EventId，再 bounded retry

**规范陈述**：CAS 冲突 → 重新观察 root → 若 EventId 已在 store 中则视为已 Committed；
否则基于新 snapshot 重建 append 并 bounded retry。retry 耗尽且 EventId 仍不在 store →
显式失败（fail closed），绝不假装已提交。

**含义/动机**：崩溃在 CAS 附近时，恢复只问「canonical store 中是否已存在这个 EventId？」
——存在即 Committed，不存在即 NotCommitted，不靠「函数有没有返回成功」猜。
**边界**：提交结局的 witness 定义见 006。
**证据**：→ PROOF.md 005。

## DURABLE-EVENTS-006 —— 提交结局的 durable witness = canonical root

**规范陈述**：提交结局的 durable witness 是 canonical root（是否已包含该 `event_id`），
不得用「再请求一次模型」、内存猜测或进程退出码代替。CAS 未见证 EventId → 不得假装已提交。

**含义/动机**：CommitUnknown 不是终点：root 本身可回答「这件事发生了没有」。
**边界**：结局未知时**如何重试/reconcile** 归 `effect-accounting`；本包只保证判定手段。
**证据**：→ PROOF.md 006。

## DURABLE-EVENTS-007 —— StorageInvalid 全局 fail closed

**规范陈述**：以下任一校验失败 → 拒绝以该 snapshot 构建投影或启动依赖它的 runtime 路径，
进入显式恢复/人工处置：坏 JSON、非 canonical、identity collision、缺 parent/成环、
payload 缺失或 hash 失配、unknown authoritative `event_type`、必填字段错误、
Append/Publish CAS retry 耗尽且 EventId 仍不在 store。**禁止**跳过坏 event 继续 fold。

**含义/动机**：第一个不可能的事件即停——跳过中间坏对象继续，后续事实就建在错基上。
**边界**：合法并发 fork 不是 StorageInvalid（见 008 与 `durable-convergence`）。
**证据**：→ PROOF.md 007。

## DURABLE-EVENTS-008 —— 并发 fork 不升级为全局 corruption

**规范陈述**：`DomainConflict`（合法并发 fork）不是 `StorageInvalid`：history 保留全部
competing facts，绝不因自然 fork 把 store 永久打成不可恢复。禁止把 DomainConflict
升级为全局 corruption；禁止「非法 fork → fail closed」的 Storage 层解释。

**含义/动机**：append-only union 必然能产生物理合法 fork；它与「全局不可恢复」必须正交。
冲突如何表达与裁决是 `durable-convergence` 的正向律，本命题只钉「不得混淆两类错误」。
**边界**：DomainConflict 的确定性表达、resolution 收敛 → `durable-convergence`。
**证据**：→ PROOF.md 008。

## DURABLE-EVENTS-009 —— 无 schema/store/migration generation；leave-unread

**规范陈述**：Store 不维护 schema/store/migration generation。旧 Journal NDJSON、
RuntimePath `blobs/`、Student QA 私有文件、feature-owned ref：不要求可读、不要求可迁、
不进入新 active domain projection、不作为 ongoing vocabulary、不要求
LegacyProjection ≡ NewProjection；runtime 永不打开（leave-unread）。禁止 dual-write、
legacy reader/importer、fallback-to-old-store shim。

**含义/动机**：clean-break 是「无版本」严谨性的来源；旧档的兼容负担在迁移期一次结清，
不进入运行时。
**边界**：迁移工具本身是 one-shot（`no-migrator` 门禁见 PROOF），不是本包 runtime 面。
**证据**：→ PROOF.md 009。

## DURABLE-EVENTS-010 —— 单一 durable substrate；唯一 canonical ref

**规范陈述**：动态 durable state 的唯一物理介质是 Git raw object database；唯一 canonical
ref 是 `refs/wanxiang/store`（指向 root tree，不是 commit）。feature-owned
`refs/wanxiang/<feature-…>`、平行 journal/blob/store 非法。AgentJournal 只是 EventStore
上的适配表面，不是平行存储；旧 NDJSON 路径不得再作为生产写入口。

**含义/动机**：一个 canonical ref、一个 object database、一套 append 协议——多进程与
dumb remote 才能共享同一套 merge/CAS/恢复。
**边界**：静态人工维护的 repository content（resources/、docs、Change 文件）不是
EventStore，仍走普通 Git。
**证据**：→ PROOF.md 010。

## DURABLE-EVENTS-011 —— Git raw 是唯一物理介质；无 commit/branch/tag 历史

**规范陈述**：Git 只作为 content-addressed object store / tree store / atomic ref CAS /
object transport。**不使用** commit history/branch/tag/merge commit 表达 EventStore 历史；
canonical ref 直接指向 root tree；树内只有 `events/<hex-prefix>/<EventId>.jsonl` 分片
与 `payloads/`；禁止 ordinal/sequence 命名。历史来自 event DAG，不来自 Git commit graph。

**含义/动机**：Store 不制造产品意义上的 Git history；root tree 只回答「当前完整
append-only event set 是什么」。
**边界**：普通 repository source history 仍走 Git commits——那是另一条语义线。
**证据**：→ PROOF.md 011。

## DURABLE-EVENTS-012 —— PayloadRef 与 payload closure

**规范陈述**：大正文经 `payload bytes → Git blob → GitObjectId → Domain PayloadRef
（opaque）→ envelope.payload_refs`。committed root 的 `payloads/` 恰好等于全部 committed
events 的 `payload_refs` 并集（closure）：dangling ref → StorageInvalid；未引用 payload
不得进入 committed root。Domain 只见 opaque `PayloadRef`，不得操作 Git OID。

**含义/动机**：closure 使「相同 event 集合 → 相同 canonical root」成立（merge 纯函数性）；
Git object id 即物理 content identity，不再维护第二套 blob 约定。
**边界**：BlobRef/BlobDigest 的兼容映射是 AgentJournal 适配细节（HOW）。
**证据**：→ PROOF.md 012。

## DURABLE-EVENTS-013 —— 查询从 projection 读；O(1) 积分；先 commit 后 fold

**规范陈述**：Projection 查询不得扫描完整历史，必须以 O(1) 积分状态回答当前
epoch/frames/coverage/XTrace 锚点/effect 窗口。Projection 不是第二真相源：禁止先改投影
再补 event；必须 append/publish 见证成功后再 fold 权威内存 projection。

**含义/动机**：把「查询」变成「重放成本」是拒绝的方案；把「先改内存再补盘」变成
「内存看见无证据的未来」。投影可随时丢弃并从 event history 重建。
**边界**：各 domain projection 的字段语义归各 domain owner。
**证据**：→ PROOF.md 013。

## DURABLE-EVENTS-014 —— 确定性 fold

**规范陈述**：投影按 `parents` 做 deterministic topological fold：任一 event 只有其全部
parents 已 fold 才可 fold；`parents` 未知/缺失/成环 → StorageInvalid。拓扑排序用 EventId
字典序作物理 tie-breaker（永不作为业务时序）。相同 merged snapshot 必须得到相同
projection。

**含义/动机**：同输入同输出是重放与审计的根基；EventId 排序只是物理 canonicalization。
**边界**：相同 event set 如何被 merge 出来 → `durable-convergence`。
**证据**：→ PROOF.md 014。

## DURABLE-EVENTS-015 —— 恢复 fold 不变量 owner（PERSIST-010）

**规范陈述**：恢复 fold 对以下事实的不变量**不满足任一条 → 拒绝 envelope，fail closed**：
`OpeningPromptCaptured`（每 lifecycle 幂等、不可覆盖）、`XTracePartAppended`（严格顺序
append-only、Cursor 单调）、`BlogEntryCommitted`（PreviousIngestCursor=当前、Next>Previous、
CoverableTurnCutoff 单调、TextDigest=blob、attempt Completed 且 terminal valid）、
`TerminalOutputCaptured`（幂等不可覆盖）、`BlogSquashCommitted`（FrameEpoch+1、不改
Ingest/Coverage）、`PrefixRebaseCommitted`（Epoch+1、candidate digest 再验证、Y bundle
PrefixCoverage-complete-turn）、`ContextReanchored`（Epoch+1、同一消息 id 只接受一次）。

**含义/动机**：本命题是 fold 的**完整性机制** owner：任何恢复事实不满足其不变量时，
fold 拒绝、writer 中毒、fail closed——不产生任何 writer 不可能产生的部分重放状态。
**边界**：各 fact 的业务语义（XTrace/coverage/epoch 的意义）归
`semantic-trace`/`work-record`/`context-compression`/`prefix-stability`/`obligation-ledger`
等 domain owner；本包只拥有「不满足即拒绝」这一条红线。
**证据**：→ PROOF.md 015。

## DURABLE-EVENTS-016 —— 所有权红线：Git 物理概念不外泄

**规范陈述**：`GitObjectId`/`RootOid`/`StoreSnapshot`/`AppendCandidate` 等物理概念只属于
Persist 层；`refs/wanxiang/store` 只允许出现在 Persist/Git infrastructure。Domain 层
不得 `open Infrastructure` 或引用这些类型；Domain 只见 `EventEnvelope` 与 opaque
`PayloadRef`。

**含义/动机**：把「事实的语义」与「事实的物理存放」隔离；领域语义不随存储机制漂移。
**边界**：这条红线的静态门禁与 fixture 见 PROOF 016。
**证据**：→ PROOF.md 016。
