# context-compression — HOW（实现模型与约束；非 normative）

## 1. 实现模型

### 1.1 恢复槽（`Domain/RecoverySlot.fs`）

- `SlotArming = NotArmed | ArmedByAdvance`：**不是**持久状态、不写 journal；是单次自动恢复
  序列的局部控制流事实。没有「offset N 是否 armed」的查询——那正是 parked-cursor 缺陷。
- `AttemptOutcome = Completed | CompletedInvalid | Failed | Aborted`：无 `Overflow` case
  （CTX-005 的结构表达）。
- `mayRecover arming offset hasMaterial`：`isArmed ∧ isRecoverySlot offset ∧ hasMaterial`
  （CTX-006 三合取）。
- `onSquashOutcome` / `onMainOutcome kind aabbConsumed` / `advancesCursor` / `nextArming`：
  RequestKind 分派（CTX-007/008）；squash 成功不推进 cursor（同一 slot 内至多一次
  `FallbackCursorAdvanced`）。

### 1.2 候选（`Domain/PrefixCandidate.fs` + `Domain/PrefixProbeSelection.fs`）

- `PrefixProbe { ProbeId; BasedOnEpochId; Candidate }` 只存在于 attempt-local 的
  `AttemptExecutionProfile.ProjectionChoice`（`UseCommittedEpoch | UsePrefixProbe`）。
  DU 而非 option：`UseCommittedEpoch` 永不能 promote，option 会把「无候选」与「槽未 armed」
  混成一个值。
- `ProviderRequestKind.mayCarryProbe`：只有 `WorkMain` 可携带 probe（CTX-009）。
- 选择（CTX-011）：候选 cutoff 严格新于 committed；identical candidate 拒绝；digest 失配
  fail closed；`requiredBlob` 按 choice 取 blob（probe 候选的 blob ≠ committed blob）。

### 1.3 Blog 投影（`Context/Companion/Blogger/Projection.fs` + `Domain/BloggerDelta.fs`）

- `BlogFrameKind = Entry | Squash`；`BlogFrame { Kind; Digest; TextRef; CoveredFrom; CoveredThrough }`。
- `BlogCoverage` 双字段：`IngestedThroughSequence`（RecordCoverage，可 mid-turn）与
  `CoverableTurnCutoffExclusive` + `CoveredPrefixDigest` + `CoverableFrameCount`
  （PrefixCoverage，完整 turn 边界）——两种证明量纲分离（CTX-015/COMPANION-011）。
- `applyEntry` / `applySquash`：squash 覆盖前半 frames（ceil half），级联可继续；
  fold 拒绝 stale frame epoch / non-sequential epoch / ingest 不前进 / coverage 回退 /
  frame count 越界（PERSIST-010，经 ContextFactFold.blogOutcome）。
- 200 KiB 分块：`BloggerDeltaLimitBytes = 200*1024`；chunker 按语义 part 边界切；
  cutoff 只在完整 turn 推进；单 part 超限硬截断并标记；omission marker 永不截断；
  instruction header 不计入 chunk 字节（CTX-013）。

### 1.4 压缩输入投影（`Domain/BloggerRequestContext.fs`、`Session/{Companion,CompanionHost,BloggerCoordinator,CompanionHostBlogger}.fs`）

- delta 可含 tool 作压缩输入；LWR gap 剔 raw tool（COMPANION-007，同源不同投影）。
- BloggerRequestMaterialized / BloggerRequestAbandoned / BlogObservationCommitted /
  BlogObservationsSquashed 四事实构成 Y 的 request cycle；`BloggerCycleProjection` 记录 receipt。

### 1.4.1 连续 catch-up：live Current → refresh → park → wake → live Current

- `BlogObservationCommitted` 后先从 canonical Blog coverage + XTrace Current 重新 `nextChunk`；有 material
  立即继续下一 ≤200 KiB cycle。不得保存 wake-time/head-time `DrainThroughSequence`、`DrainFrontier`、
  target head 或等价 frozen upper bound。
- 当前 refresh 返回 None 只说明**此刻** caught-up。若 main 未合法终止，`ParkTransform` 保持当前
  continuation 悬挂；`PendingOffer` 只负责唤醒，不作为下一块内容权威。
- wake 后丢弃 stale offer，重新读取 live Current 并 `RefreshMainContext`。因此 park 期间新增、sequence
  超过 park 前 XTrace head 的 material 仍属于同一连续 catch-up，必须立即进入下一 cycle。
- 这条路径使用 F# CE `let!`/`match!` 直接表达等待与继续；不维护 Stage/PC，不构造 drain state machine，
  不扫描/重放 Journal。业务读取只用 canonical Integrator 已维护的 Current（DURABLE-EVENTS-019）。
- quiet 不是直接 stop：normal commit、idempotent receipt、stale catch-up、protocol-repair re-entry 必须汇合到
  同一个 `ParkTransform` 边界。在同一存活执行内先 park，只有 durable seal / cancel 或 park waiter 既有
  physical lifetime 才能解除等待；这些是既存终止/物理边界，不得被解释成“caught-up 已完成”的业务判据。
- process death 直接中断旧 tool/continuation；普通 Host restart 不重新挂起这个 waiter、不 replay 旧 cycle、
  不补 terminal。跨进程语义完全服从 CRASH-017/018；显式 `/continue` 也不续跑旧 Blogger invocation。

### 1.5 Host compaction containment（`Domain/HostCompactionPolicy.fs`）

- 预防层：`compaction.auto` / `compaction.prune` / `compaction.autocontinue` 必须为 false，
  无法证明关闭 → `HostContractUnsupported` 启动失败。
- 收容层：任意观察到的 compaction pseudo-run → 原子 `ContextReanchored`（HOST-006）；
  `nextReanchor` 消费 `PrefixEpochProjection.isReanchored`（同 compaction 只重锚一次）。
- `prune` 特殊：绕过 transform 直接删行，收容层无法修复 → 必须预防关闭。

### 1.6 诊断边界（`Kernel/Diagnostic`，见 ctx014 测试）

- `Diagnostic.emit` 只接受白名单字段；未知字段 → fatal。
- 禁止字段一旦出现在 production source → `ctx014.test.mjs` tombstone 拦截。

## 2. 与相邻包的分工

| 机制 | owner |
|---|---|
| 候选 epoch 提升（`PrefixRebaseCommitted`/`ActivePrefixEpoch`） | prefix-stability |
| XTrace 事实源 / cursor | semantic-trace |
| TOML 布局渲染 | provider-projection |
| armed/primed（FALLBACK-012）、失败预算 | provider-attempt-recovery |
| `ContextReanchored` 的 epoch 语义 | prefix-stability（本包只拥有「什么观察触发重锚」） |

## 3. 已知非目标（HOW 层）

- 200 KiB 数值当前是合同（CTX-003），但 card 注明「只有被证明是产品合同的上界才进入未来
  WHAT」——若未来有更强合同，数值可演进。
- squash 的「前半 frames」策略（ceil half）是当前算法；「squash 只处理本 X frames」
  才是命题（CONTEXT-COMPRESSION-014）。
- `CoverableFrameCount` 是对 `CoverableBRef` 的等价压缩（append-only frame 列表内可推导），
  是 HOW；「probe 只用 cutoff 前覆盖 frames」才是命题。

## 4. 历史与弃权

### 4.1 源 → 覆盖映射

| 源 | 信息落点 |
|---|---|
| 历史 why/what context（CTX-001..016） | WHAT-001..017；WHY §4 |
| 历史 how/context | HOW §1.2/1.3/1.5 |
| 历史 shape/context | HOW §2（coverage 读边界、ActivePrefixEpoch 所有权） |
| 历史 COMPANION-005/006/007/008/013 | WHAT-010/011/012/014/015/016 |
| 历史 HOST-006 | WHAT-002；HOW §1.5 |
| 历史 requirements-design card（13-context-continuity） | 全部 OWNS/DOES NOT OWN 裁决 |
| 历史 COVERAGE（CTX-* 行） | WHAT 命题归属（CTX-011 → prefix-stability + →context-compression 的交界） |

### 4.2 弃权（GARBAGE / 明确不归本包）

- **`session.compacted` 冒充 TodoCheckpoint**：shape/context.md 明确禁止；epoch 语义归
  prefix-stability，本包不重复。
- **`NeedRebase` / `RebaseRequested` Stage 与 todo-only 平行 epoch**：CTX-015/TODO-009 拒；
  是 GARBAGE（被拒方案），本包不立命题。
- **`PrefixProbeRolledBack`**：被拒方案（CTX-010）；本包以「失败无事实」表述，不发明回滚事实。
- **「按容量切 epoch」**：被拒方案（COMPANION-009 考古）；由 prefix-stability 的冷边界
  三证据源覆盖，本包不重复。
- **`context_ratio` 式诊断字段**：X9 已删；ctx014 tombstone 保留为机制（本包 CONTEXT-COMPRESSION-013）。

## 5. 依赖理由（DEPENDS ON）

- `semantic-trace`：ingest cursor 是 XTrace 游标；delta 与 gap 同源。
- `provider-projection`：压缩结果（TOML delta、prefix projection）是 provider 表示；
  本包不拥有渲染。
