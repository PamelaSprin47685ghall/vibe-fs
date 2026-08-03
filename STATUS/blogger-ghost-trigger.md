# Blogger 幽灵触发 — 修订计划

状态：已实施（worktree-01）  
取代：先前「调查-only / dispose-on-idle」叙事  
用户裁决（2026-08-03）：

1. **coverage 不推进默认致命**；provider error 与已知可恢复原因走 **AABB**，不杀进程。  
2. **main 已 join/return 后，Blogger 不准再发新请求**（再跑无意义）。

---

## 0. 现状铁证（改前共识）

| 事实 | 证据 |
|---|---|
| 无显式 job 队列 | `onMainMaterial`：InFlight→Skip；Parked→单槽 Offer |
| 唯一新 Start/Offer | 非 Y 的 `messages.transform` → `hasMaterial` → `onMainMaterial` |
| join/return **不**停 Y | `HandleCompleted` / `HandleRetired` 不碰 Companion；`session.deleted` 才 `DisposeSession` |
| coverage 不走仍可再雇 | 多路 `bestEffortEnd` → Idle → 下次 transform 再 `Start` |
| `nextIngest <= prev` | 已是 `unexpectedEnd` → `Diagnostic.fatal` |

idle 后无人 transform ⇒ 不点火：仍正确。  
问题是 **join/return 后仍可能 transform/再 Start**，以及 **失败 Idle 后同一 gap 再雇**。

---

## 1. 规则 A — main returned/joined ⇒ 禁止 Blogger **新请求**

### 1.1 「return/join」的操作定义（两条会话族）

**禁止**用「idle」当停止条件（用户已否）。

| main 形态 | 已 return/joined 的 durable 判据 | 备注 |
|---|---|---|
| **Fork child**（有 handle） | `HandleByChildSession[main]` 的 lifecycle ∈ `{ CompletedAwaitingJoin, Retired }` | join 前 CompletedAwaitingJoin 已对父可消费；Retired 为 join 后 tombstone。两者都表示 **子工作单元已终态交付**，Y 再写 frames 进不了已冻结的 join LWR |
| **Human root / 无 handle** | **不**用 handle。采用：**本 session 作为 parent 时不再有 Active handle，且自身不是任何 parent 的 Active child** 不够。更干净：Human root **没有** join 语义；停止条件改为 **session.deleted** 或显式 **「逻辑 run 终态」**——见下 |

Human root 修订（相对口头「join/return」）：

```text
Human root 没有 join。
「return」= 对该 X 而言工作结果已不再有消费者：
  - session.deleted（已有 DisposeSession），或
  - 可选加强：ActiveLogicalRun 结束且不再接受非 continuation 的 Authority（易过度，默认不做）
默认实施：Human root 只靠 rule B（coverage 致命）+ 既有 deleted 清理；
Fork child 强制 rule A（CompletedAwaitingJoin | Retired → 禁新 Y 请求）。
**再 prompt 解封**：新 Authority Root（Human/AgentOwner）经 PromptIngress → `reactivateAfterNewRoot`，
`ReactivatedAfterSeal=true`，即使 durable handle 仍 joinable/retired 也可再 Start，直到下一次 HandleCompleted 再封。

```

若产品要坚持 Human root 也有「事做完停 Y」：必须另立 **显式 durable 事实**（例如 `WorkSessionSealed`），禁止猜 idle。本计划 **不发明** 该事实，除非你后续点名。

### 1.2 禁止的是什么

```text
禁止：StartFromContext（新 prompt_async）
禁止：SetPendingOffer（新材料 staging，会 ResumeParked 开下一轮）
允许：已 InFlight 的当前 cycle 跑完 commit（不 abort 半截 tool-loop）
允许：commit 后若已有 PendingOffer——若 main 在 InFlight 期间刚 Completed？
```

**PendingOffer 竞态**：main 在 Y InFlight 时 CompletedAwaitingJoin，此前 Parked 路径可能已写入 Offer。

裁决：

```text
adoptPending / resumeWithContext 前再查 rule A
若 main 已 CompletedAwaitingJoin|Retired → 丢弃 Offer，不 resume，CancelParked，runtime → 静默停机态（见 1.4）
不 Start 新请求
```

### 1.3 闸点（唯一入口收口）

| 位置 | 行为 |
|---|---|
| `BloggerCoordinator.onMainMaterial` 入口 | `mainSealedForBlogger mainId` → `DecisionEffect.Sealed`（新 case），**不** Start/Offer |
| `startFrozen` 发送前 | 双检；已 sealed → 不 `SendAgentOwnerRoot`，abandon open materialize |
| `EnforcerHost` commit 成功后 take PendingOffer / park resume | sealed → 丢 Offer，不 `resumeWithContext` 新 Main ctx |
| `SetPendingOffer` | 可在 coordinator 侧根本不调用 |

查询：

```fsharp
// TerminalPolicy 或 Domain 纯函数
let mainSealedForBlogger (journal: AgentJournal) (mainSessionId: SessionId) : bool =
    match Map.tryFind mainSessionId snapshot.HandleByChildSession with
    | Some { Lifecycle = CompletedAwaitingJoin _ | Retired } -> true
    | Some { Lifecycle = Active } -> false
    | None -> false   // Human root：本规则不密封（仅 fork child）
```

`HandleByChildSession` 已是 PERSIST-008 索引（`TerminalPolicy.tryLinkedChild` 同源）。

### 1.4 密封后的 runtime 态

不用 Disposed（与 session.deleted 混淆）。

```text
Sealed（或复用 Idle + 旁路标志）
  - onMainMaterial → Sealed / NoMaterial 等价，零发送
  - 不 Arm 新 recovery 开跑
  - 已 InFlight：允许当前 cycle 结束；结束后不 park 等货，直接静默 Idle/Sealed
```

实现偏好：`BloggerRuntimeState` 增 `Sealed`，或 `onMaterial` 在 sealed 时 `Ignore`。增 case 更可读。

### 1.5 与 catch-up（H3）关系

- main **仍 Active**：允许 park/resume 追 gap。  
- main **已 CompletedAwaitingJoin**：join LWR 已在 complete 时物化；**再追 gap 写 Y frames 改变不了已交付 LWR** → 禁新请求。  
- 若希望「complete 前必须 Y 追平」：那是 **complete 路径** 的 `WaitInFlight` 问题，**另项**；本规则不在 join 后补课。

---

## 2. 规则 B — coverage 不推进：默认致命，AABB 例外

### 2.1 分类

| 类 | 例 | 处置 |
|---|---|---|
| **Invariant / 协议崩** | `coverage did not advance`；`delta digest mismatch`；`missing CurrentRequest`；live blog 无 authority；commit 后 ingest 未前进；validateCycle 非空-text 的硬错误 | `Diagnostic.fatal` + stderr 现场 + 停机 |
| **Provider / 模型已知方差（AABB）** | 空 text（ENFORCER-061）**一次** repair；纯 prose 无 blog（ENFORCER-060）**一次** repair；`blog tool interrupted`（abort 清理） | **不** fatal；同 InFlight 内 AABB；耗尽后 **fatal**（不再 bestEffort→Idle→再雇） |
| **传输/Host 层** | Y 的 `TurnFailed` provider error；Host retry | 走既有 Fallback/AABB（cursor odd 槽 + ArmRecovery）；**不**把「coverage 没动」当成功结束；若 cycle 结束时仍无 commit → 不得静默 Idle 后再 Start 同一 RequestId 材料，除非新的 Main 材料（新 delta）且 main 未 sealed |
| **CommitUnknown** | journal 未知 | 已 fatal，保持 |

### 2.2 AABB 含义（本项）

```text
A 路失败（可分类）
  → 同「追赶回合」内至多一次可恢复重试（repair / provider AABB）
  → 仍无有效 blog commit
  → fatal（stderr：session_id, blogger_session_id, prev/next ingest, delta_digest, result）
禁止：
  onFail → Idle → 下一次 main transform 用同一 prev_ingest 再 StartFromContext
  用失败时冻结的旧 Toml / 旧 RequestId / 旧 next_ingest 原样重放
```

可恢复只在当次追赶回合内；用 Idle 跨请求「再排队同一 gap」= 禁止。

### 2.2.1 AABB 重试必须吃进最新进展（用户补充）

**不要**拿失败那一刻 materialize 的 delta 原样再发。  
失败 → 真正再次让 Y 看见 New Work 之间，main/X 可能又往前走；重试应把这段也打进同一块（仍受 200KiB），追赶更快。

```text
coverage 仍停在 prev = IngestedThrough（失败未 commit ⇒ 未前进）
重试前：
  1. 读 main 当前 projection + 当前 XTrace
  2. nextChunk(limit, cursor(prev), cutoff, latestMessages)
  3. 得到新 Toml / 新 next_ingest / 新 DeltaDigest / 新 RequestId
  4. 替换 InFlight / CurrentRequest /（如需）durable materialize supersede
  5. 再发：InteractionRepair 注入或 provider 重试，rebuild 用【新】ctx
```

| 项 | 旧（禁止） | 新（要求） |
|---|---|---|
| New Work TOML | 失败时冻结的 `main.Toml` | 自 **同一 prev** 切到 **最新 head** 的 chunk |
| RequestId / DeltaDigest | 复用 | 随新 Toml 重算 |
| prev_ingest | 不变 | 不变（仍未 commit） |
| next_ingest | 冻结旧值 | 随最新 chunk 可前进更远 |
| Working Record frames | 不变 | 不变（无新 commit） |
| 次数 | 同回合 ≤1 次 AABB | 同左；耗尽 fatal |

与 rule A 合取：重试前若 `mainSealedForBlogger` → **不**重试、不发新请求（丢 InFlight，静默 Sealed）。

实现落点：

- ENFORCER-060/061 注入 repair **之前**：`refreshMainContextFromLatest`（coordinator/EnforcerHost 共用）。  
- Y `TurnFailed` 后若走 companion 侧再 materialize：同一函数，禁止 `StartFromContext(oldCtx)`。  
- `PendingOffer` 路径本就每次 `nextMainContext` 重算——保持；不要在 resume 时沿用过期 Offer 而不重算（resume 前可再 refresh 一次最新 projection）。

### 2.3 具体改 `bestEffortEnd` 分流

| 当前 bestEffort 理由 | 新处置 |
|---|---|
| `protocol-repair-exhausted` / ENFORCER-060 二次 | **fatal** |
| `blog tool interrupted without completed call` | 保持可恢复一次；若之后无新 commit 且再入口 → fatal 或 ignore if sealed |
| `KnownNotCommitted`（非 provider） | **fatal**（带 reason） |
| `park-timeout` | main 未 sealed：可视为无新材料，**Idle 且不强制 fatal**；若 timeout 时仍 hasMaterial 且 coverage 同 → **fatal**（有货却 10min 没人喂 = 异常） |
| 空 text 首次 | 保持 repair（AABB） |

`coverage did not advance`：保持 fatal（已有）。

### 2.4 stderr 现场字段（CTX-014 白名单内）

已有：`session_id`, `blogger_session_id`, `result`, `delta_bytes`, `cutoff_before/after` …  
提交 fatal 时尽量带：`result`, `session_id`, `blogger_session_id`；若需 prev/next ingest，**扩展白名单**并改 `ctx014.test.mjs`（或塞进 `result` 字符串避免扩表——优先结构化扩表：`cutoff_before` 已有，可复用语义或加 `result` 内嵌）。

最小：`result` 含 `coverage-stall prev=… next=… digest=…`。

---

## 3. 非目标

- main idle ⇒ dispose Y（不作为主修复）  
- 删除 park/resume 有限 catch-up（main Active 期间仍要）  
- Human root 猜「用户觉得做完了」  
- 改 join LWR 物化时机（WaitInFlight 另项）

---

## 4. 实施步骤

1. **纯函数** `mainSealedForBlogger` + 单测（Active / CompletedAwaitingJoin / Retired / 无 handle）。  
2. **`BloggerRuntime` / coordinator**：sealed 短路；`DecisionEffect.Sealed`。  
3. **`refreshMainContextFromLatest`**：AABB/repair 前自 prev 重切最新 projection；更新 InFlight+materialize。  
4. **EnforcerHost**：PendingOffer/resume 前 sealed 检查；resume 前可选再 refresh；`bestEffortEnd` 分流 fatal。  
5. **park-timeout**：hasMaterial∧未 sealed∧coverage 未变 → fatal；否则静默。  
6. **CTX-014 / tests**：bestEffort→fatal 分流；sealed 零发送；repair 后 delta_digest/next_ingest 随最新 head 变。  
7. **更新**本文件状态 → 已实施 + commit。

---

## 5. 测试矩阵

| 用例 | 期望 |
|---|---|
| child Active + gap | 可 Start |
| child CompletedAwaitingJoin + gap + transform | **不** Start/Offer |
| child Retired + gap | **不** Start |
| Human root 无 handle + gap | 仍可 Start（本规则 A 不密封） |
| InFlight 中 CompletedAwaitingJoin | 当前 cycle 可 commit；其后不 resume Offer |
| `next <= prev` | fatal（已有） |
| 空 text 首次 | repair，非 fatal；**New Work 为最新 chunk**（失败后 main 又增长则 toml/digest/next 变） |
| 空 text 首次 + 失败后无新 X | repair 仍可同内容重切（digest 可同），但必须走 refresh 路径而非裸复用 ctx 指针 |
| 空 text 二次 | fatal |
| KnownNotCommitted 协议错误 | fatal |
| sealed 后 onMainMaterial | Sealed，零 `SendAgentOwnerRoot` |
| sealed 后 AABB 将触发 | 不重试、不发送 |

---

## 6. 风险

| 风险 | 缓解 |
|---|---|
| complete 时 Y 未追平 → join LWR RawGap 大 | 另项：complete 前 `WaitInFlight`；本规则故意不 join 后补课 |
| Human root 仍可能幽灵 Y | 仅靠 B + deleted；若不够再立 `WorkSessionSealed` 事实 |
| fatal 过宽误杀 canary | `WANXIANGSHU_NO_FATAL_EXIT` / `NODE_TEST_CONTEXT` 已闸；测 fatal 用 console.error 捕获 |
| Sealed vs Disposed 双态 | 文档写清：deleted→Disposed；join→Sealed |

---

## 7. 验收

- 理论命题「join 后还能新请求」→ **否**（fork child）。  
- coverage 不推进的跨请求重雇 → **进程 fatal**。  
- provider/ENFORCER-060/061 首次 → AABB，不 fatal。  
- `gate:static` + 定向 `enforcer-cycle` / 新 sealed 测绿。
