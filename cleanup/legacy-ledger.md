# Compatibility Ledger — Operation Clean Slate（Refactor Closure）

> 临时工作台：本文件是 cleanup 期间唯一兼容性台账。**cleanup 完成后本文件自己必须删除。**
> 规则：不允许 `UNKNOWN → KEEP`；只能 `UNKNOWN → investigate → DELETE` 或
> `UNKNOWN → investigate → BOUNDED-COMPAT`。无证据即 DELETE。
> 每解决一部分，编辑 TASK.md 标完成 → `git commit`。

## 台账原则（TASK.md §二）

| 原则 | 内容 |
|---|---|
| 分类 | DELETE / MIGRATE / BOUNDED-COMPAT |
| 无证据 | → DELETE |
| Exit condition | 每项必须写明「什么事实成立后它必须消失」 |
| 删除预算 | 优先删无 caller 的过渡 API、死壳、no-op vocabulary |

---

## Wave 1：死壳 / no-op / 无 caller transition API

### LEGACY-001：`ManagerActivation` 模块（no-op vocabulary）

| 字段 | 值 |
|---|---|
| Surface | `src/Wanxiangshu/Mission/Manager/Activation.fs`（module `ManagerActivation`） |
| Current owner | `Mission/Manager/Activation.fs` |
| Old world | 旧 Manager activation（GLORY-018 生产 path 已删，`ensureAccepted` 是 no-op，只返回 `Ready`/`Deferred`） |
| Current consumer | 无生产调用点。全仓精确搜索 `ManagerActivation.ensureAccepted` 仅 HOW 文档命中 |
| Consumer evidence | `requirements/structured-workflow/HOW.md:41,154`（文档引用）；`requirements/structured-workflow/tests/semantic-vocabulary.test.mjs:22`（vocabulary pin）；`scripts/checks/dsl-ownership.mjs:151`（HOST_BOUNDARY_OPEN_BASENAMES 死条目）；`src/Wanxiangshu/Wanxiangshu.fsproj:382`（编译引用） |
| Writer alive? | 否（`ensureAccepted` 不写任何 fact） |
| Reader alive? | 否（无生产读） |
| Classification | **DELETE** |
| Exit condition | 删除模块 + 测试引用 + HOW 引用 + dsl-ownership 死条目 + fsproj 引用后，全仓 `rg 'ManagerActivation'` 零命中 |
| Owner | 本仓库 |
| Removal PR | CLN-02 |

### LEGACY-002：`RunCompletion.AgentId`（DEPRECATED 字段）

| 字段 | 值 |
|---|---|
| Surface | `RunCompletion.AgentId: string`（`Execution/Session/AgentCompletion.fs:211-213`，注释「DEPRECATED: Kept for HostFork* backward compatibility. New code should use the Map key or AgentName」） |
| Current owner | `Execution/Session/AgentCompletion.fs` |
| Old world | HostFork 时代用 `AgentId` 标识 run owner；新世界用 Map key / AgentName |
| Current consumer | 3 个 F# read site + 多个测试断言 |
| Consumer evidence | F#：~~`JoinDrain.fs:339`~~、~~`JoinResultRenderer.fs:237,255`~~（已迁 `AgentCompletion.agentId`）；写入点：`AgentCompletion.fs:251,258`（PTY 三 case 均 `AgentId = id`）、`Cohort.fs:209,285`、`ChildRun.fs:133,141`。测试：~~`managed-session-lifecycle/tests/child-run-projection.test.mjs:125`~~、~~`host-fork-agent.test.mjs:163`~~、~~`host-fork-restart-lifecycle.test.mjs:155`~~、~~`host-fork-runtime.test.mjs:99,240,253,276`~~、~~`verification-system/tests/support/domain/orchestrator.mjs:58,360,400,718`~~（已迁 `agentIdOf`） |
| Writer alive? | ~~是（`toRunCompletion` PTY 投影仍在写）~~ → 否（字段已删） |
| Reader alive? | ~~是（上述 read sites）~~ → 否（全部迁移） |
| Classification | **DELETE**（先迁 caller）→ **已删除（CLN-04）** |
| Exit condition | ~~first-party read sites → 0 → 删字段~~ → **达成**：`RunCompletion.AgentId` 全仓零命中（F# + JS） |
| Owner | 本仓库 |
| Removal PR | CLN-03（迁 caller）/ CLN-04（删字段） |

### LEGACY-003：`RunCompletion` single-result Join compatibility（`migrationJoinOutcome`）

| 字段 | 值 |
|---|---|
| Surface | `JoinDrain.migrationJoinOutcome`（`Execution/Delegation/Handle/JoinDrain.fs:202`，把 `Result<unit, ForkError>` 投影成 `Result<RunCompletion, ForkError> option`）；配套 `migrateOutcomeToUnit`（:415） |
| Current owner | `Execution/Delegation/Handle/JoinDrain.fs` |
| Old world | Join API 曾经返回 single-result `RunCompletion`；新世界 `JoinItem` 是 canonical representation |
| Current consumer | `JoinDrain` 内部 `tryMigrateRetiredFalseAbort` → `migrateOutcomeToUnit`（:435）；`JoinDrain` 外部 `Restart.fs:146` |
| Consumer evidence | `JoinDrain.fs:208-218`（`migrateRetiredFalseAbort` 内部）、`JoinDrain.fs:415-435`（`migrateOutcomeToUnit` 链）、`Execution/Delegation/Fork/Host/Restart.fs:146`（`tryMigrateRetiredFalseAbort`） |
| Writer alive? | 是（`migrationJoinOutcome` 仍是 join 路径的一部分） |
| Reader alive? | 是（`tryMigrateRetiredFalseAbort` 消费） |
| Classification | **MIGRATE → DELETE**（迁 caller 到 canonical `JoinItem`） |
| Exit condition | `JoinDrain` 不再构造 `Result<RunCompletion, ForkError>` single-result 路径 → 删 `migrationJoinOutcome`/`migrateOutcomeToUnit` → join 只消费 `JoinItem` |
| Owner | 本仓库 |
| Removal PR | CLN-05（迁 caller）/ CLN-06（删兼容路径） |

---

## Wave 4：Persistence compatibility（FactCodec census，先分类不删）

> 原则：`OLD bytes → one decoder → CURRENT domain` 允许；`OLD bytes ↔ OLD model ↔ adapter ↔ CURRENT model` 禁止。旧物理 store：不读、不迁、不 reset、不双写；禁止 legacy importer / migrator / fallback-to-old-store shim。**refusal boundary（pre-0.5.0 reject / ScoreVectorRef-era reject / unanchored Guideline reject）不是兼容债，可保留。**

### LEGACY-010：`FactCodec.migrateHandleCompleted`

| 字段 | 值 |
|---|---|
| Surface | `Persistence/Journal/FactCodec.fs:218`（`private migrateHandleCompleted`，缺字段时注入 `null`） |
| Current owner | `Persistence/Journal/FactCodec.fs` |
| Old world | 旧 `HandleCompleted` 记录缺字段 |
| Current consumer | `deserializeFact` pipeline（:274） |
| Consumer evidence | durable sample：`requirements/durable-events/tests/fact-codec.test.mjs`（migration 测试） |
| Writer alive? | 否（新 writer 写完整字段） |
| Reader alive? | 是（`deserializeFact` 自动注入 `null`） |
| Classification | **待 census**（有真实 durable sample 则 BOUNDED-COMPAT decode-only；无则 DELETE） |
| Exit condition | 无真实旧数据 → DELETE；有真实旧数据 + 必须支持 → KEEP decode-only + retention horizon |
| Owner | 本仓库 |
| Removal PR | CLN-08..N |

### LEGACY-011：`FactCodec.migrateHandleOwnership`

| 字段 | 值 |
|---|---|
| Surface | `Persistence/Journal/FactCodec.fs:233` |
| Current owner | `Persistence/Journal/FactCodec.fs` |
| Old world | 旧 `HandleOwnership` 记录缺字段 |
| Current consumer | `deserializeFact` pipeline（:275） |
| Consumer evidence | durable sample：fact-codec.test.mjs |
| Writer alive? | 否 |
| Reader alive? | 是 |
| Classification | **待 census** |
| Exit condition | 同上 |
| Owner | 本仓库 |
| Removal PR | CLN-08..N |

### LEGACY-012：`FactCodec.migrateHandleByname`

| 字段 | 值 |
|---|---|
| Surface | `Persistence/Journal/FactCodec.fs:245` |
| Current owner | `Persistence/Journal/FactCodec.fs` |
| Old world | 旧 `HandleByname` 记录缺字段 |
| Current consumer | `deserializeFact` pipeline（:276） |
| Consumer evidence | durable sample：fact-codec.test.mjs |
| Writer alive? | 否 |
| Reader alive? | 是 |
| Classification | **待 census** |
| Exit condition | 同上 |
| Owner | 本仓库 |
| Removal PR | CLN-08..N |

### LEGACY-013：`FactCodec.migrateManagerJobByname`

| 字段 | 值 |
|---|---|
| Surface | `Persistence/Journal/FactCodec.fs:256` |
| Current owner | `Persistence/Journal/FactCodec.fs` |
| Old world | 旧 `ManagerJobByname` 记录缺字段 |
| Current consumer | `deserializeFact` pipeline（:277） |
| Consumer evidence | durable sample：fact-codec.test.mjs |
| Writer alive? | 否 |
| Reader alive? | 是 |
| Classification | **待 census** |
| Exit condition | 同上 |
| Owner | 本仓库 |
| Removal PR | CLN-08..N |

### LEGACY-014：`FactCodec.rewriteLegacyObservationTags`

| 字段 | 值 |
|---|---|
| Surface | `Persistence/Journal/FactCodec.fs:92`（`public`，Envelope.fs:93 也引用） |
| Current owner | `Persistence/Journal/FactCodec.fs` |
| Old world | 旧 Observation tag 形状（tag rewrite） |
| Current consumer | `deserializeFact` pipeline（:278）+ `Envelope.fs:93`（Decode 前 rewrite） |
| Consumer evidence | durable sample：fact-codec.test.mjs |
| Writer alive? | 否 |
| Reader alive? | 是（decode 前置步骤） |
| Classification | **待 census** |
| Exit condition | 同上 |
| Owner | 本仓库 |
| Removal PR | CLN-08..N |

---

## Wave 5：有债权人的 compatibility（不删，关进隔离区 + exit condition）

### LEGACY-020：`Host TodoTable compatibility sink`

| 字段 | 值 |
|---|---|
| Surface | `MagicTodoProjection / Journal facts = canonical truth`；`Host TodoTable = compatibility sink only` |
| Current owner | `host-boundary` |
| Old world | OpenCode Host V1 TodoTable |
| Current consumer | OpenCode Host V1 TodoTable（具名债权人） |
| Consumer evidence | HOW 明确：服务当前 Host V1；canonical truth 不依赖它 |
| Writer alive? | 是（sink 单向投影 canonical → V1） |
| Reader alive? | 否（禁止 V1 → canonical 反推） |
| Classification | **BOUNDED-COMPAT**（有具名债权人） |
| Exit condition | Host V1 TodoTable 不再属于 supported host contract → 删 `Surface.CompatibilityTodoRow` / `obligationsToCompatibilityRows` / V1 canaries |
| Owner | `host-boundary` |
| Removal PR | CLN-Y |

---

## Wave 6：runtime migration（migration amnesty review）

### LEGACY-030：`JoinDrain.migrateRetiredFalseAbort` / `tryMigrateRetiredFalseAbort` / `migrateOutcomeToUnit`

| 字段 | 值 |
|---|---|
| Surface | `Execution/Delegation/Handle/JoinDrain.fs:208-218,415-435,465` |
| Current owner | `Execution/Delegation/Handle/JoinDrain.fs` |
| Old world | Retired legacy false abort（确定性 replacement + correction，idempotent） |
| Current consumer | `JoinDrain` join 路径 + `Restart.fs:146`（重启时尝试迁移） |
| Consumer evidence | `Execution/Delegation/Fork/Host/Restart.fs:146` |
| Writer alive? | 否（新世界不制造 false abort blob） |
| Reader alive? | 是（join 时 detect → reconstruct → rewrite → continue） |
| Classification | **待 census**（问：修复哪个版本前数据？新版本还会制造吗？坏数据有限集合？能否离线一次性 repair？observable evidence 是否坏数据为零？） |
| Exit condition | 无坏数据样本 → DELETE；有 → detect → refuse（不 rewrite），或 offline repair 替代 runtime migration |
| Owner | 本仓库 |
| Removal PR | CLN-08..N / CLN-X |

---

## 附录：`unified-store-gate` 去考古化（TASK.md §十）

- 现保留：Student QA revival / no-migrator / legacy importer / dual-write 历史 token gate（2026-08-14 retired 注释）。
- 终态：拆成「历史 token gate → 逐步淘汰」+「永久 architecture invariant → 保留」。
- Exit：新世界基线稳定后删除 absence ratchet，靠 capability ownership rule / role projection rule / type system / positive architecture gate 防复活。
