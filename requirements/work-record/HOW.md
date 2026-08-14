# work-record — HOW（实现模型与约束；非 normative）

## 1. 实现模型

### 1.1 类型层（`src/Wanxiangshu/Domain/LifecycleWorkRecord.fs`）

```fsharp
type OpeningMaterial =
    { AssignmentText: string            // InitialCharge（OpeningPromptCaptured 的 inline 副本）
      AuthoritativeRequirements: string list
      ConstitutiveBody: string }        // BlindPlan 区间（经 XTrace.forOpening 渲染）；Immediate 为空

type LifecycleWorkRecord =
    { Opening: OpeningMaterial          // 永远保留（WORK-RECORD-006）
      Frames: string list               // Chronicle = 已解析的 Y frame 文本
      Gap: XTraceItem list }            // Recent work = 未覆盖 suffix（须已 forWorkRecord）
```

- `OpeningPolicy.immediate` / `forManager = BlindPlan FirstPlanCompleteTodoWrite`；Manager Opening 只在第一次 accepted `planComplete=true` 后关闭。
- `render includeOpening record`：三段纯文本 Markdown；空段整段省略；`includeOpening=false`
  省略 Opening；段标题为纯文本 `Opening` / `Chronicle` / `Recent work`，`# ` 仅由
  `SyntheticToml.comment` 在 wire 注入（避免 `# # Chronicle`）。
- `materialize opening frames trace coverage openingEnd includeOpening`：
  `gapStart = { Sequence = max coverage.IngestedThrough.Sequence openingEnd.Sequence }`；
  gap = `XTrace.sliceFrom gapStart trace |> XTrace.forWorkRecord`（WORK-RECORD-005/013）。
- `withConstitutive`：把 BlindPlan constitutive 区间渲染进 `ConstitutiveBody`（WORK-RECORD-009）。

### 1.2 bounded range（`src/Wanxiangshu/Domain/MagicTodoLwr.fs`）

```fsharp
type BoundedRange = { StartInclusive: XTraceCursor; EndExclusive: XTraceCursor }
```

一个 invocation / request 的排他 XTrace 范围（EXEC-031）。Start 常为 WorkRecordStart /
invocation send head；End 为 ReviewFrontier / invocation completion head。

### 1.3 物化（`src/Wanxiangshu/Application/Finality/LifecycleWorkRecordProjection.fs`）

- `lifecycleWorkRecordFromSnapshot durable snapshot sessionId includeOpening coverageOverride`：
  full-lifecycle 物化。解析 frames（digest 校验失败即丢弃该 frame）、解析 trace parts
  （media_omitted 保留为 omission marker）、`withTerminalFallback` 在最新 assistant turn
  未含 terminal 字节时把 terminal 投影进 Recent work（不写新 XTrace 事实）。
  `openingEnd` 由 `ManagerOpeningFloor.workRecordStart` 推导；无 Life 时 = 第一条 part 之后。
- `lifecycleWorkRecordBoundedFromSnapshot durable snapshot sessionId range`：bounded 物化。
  frames 按 `(Previous, Next]` 与 `[Start, End)` 重叠过滤；trace 按 range slice；
  coverage 夹到 range 内（`max(…, range start)` / `min(…, range end)`）；`includeOpening=false`。
- full 与 bounded 共用 `LifecycleWorkRecord.materialize` —— 单一 renderer（WORK-RECORD-010）。

### 1.4 floor（`src/Wanxiangshu/Journal/ManagerOpeningFloor.fs`）

- `workRecordStart life magic xTrace`：Post-T1 = `MagicTodo.blindPlanOpeningBoundary`
  （首次 true 的 T1 call cursor + callId + part anchors）；此前任意 false planning checkpoints 仍属于 Pre-T1 Opening。
- `effectiveOpeningFloor`：Life 未开 / 已 Completed → None；否则按 acceptedCount 与 T1 anchor
  推导。**从不读** `WorkActivated` / `ProtectedPrefixEnd`（TODO-001 考古）。
- `floorSequence`：session helper，供 BloggerCoordinator / CompanionTransform 的
  effectiveStart = `max(RecordCoverage, floor)`。

## 2. 消费方

| 消费方 | 消费什么 |
|---|---|
| `delegation`（EXEC-004/028/031） | 子→父 / SyncDelegate 的 bounded record（includeOpening=false） |
| `review-assurance` / `review-judgement`（REVIEW-016） | ProcessReviewLWR（RecordCoverage + RawGap） |
| `finality`（GLORY-004/050） | FinalityReviewLWR（request-range bounded） |
| `obligation-ledger`（TODO-006/008） | ManagerCheckpointLWR（ReviewFrontier） |
| `prefix-stability` / `context-compression` | 只引用 coverage 分型，不把 LWR RawGap 当 prefix 证明 |

## 3. 已知非目标（HOW 层）

- `withTerminalFallback` 是 Host 边界 fallback HOW（consumption 前 terminal 已 durable）；
  「Terminal 不是 LWR 段」才是命题（WORK-RECORD-011）。
- 段标题字面（`Opening` / `Chronicle` / `Recent work`）当前是渲染事实；card 明确
  「当前三段标题字面不必须永久不变」——renderer 可整体重写，只要 boundedness / Opening /
  coverage 分型 / prose-claim 不变。
- `OpeningMaterial.AssignmentText` 是 OpeningPromptCaptured 的 inline 副本：它是 captured
  事实的**物化输入**，不是从 Assignment/requirements 文本重建的第二事实源（WORK-RECORD-008
  禁止的是「reconstruct」，即把 record 的 Opening 当可拼装物）。

## 4. 历史与弃权

### 4.1 源 → 覆盖映射

| 源 | 信息落点 |
|---|---|
| `archive/docs/what/companion.md` COMPANION-003/014/015 | WHAT-001..013；HOW §1.1 |
| `archive/docs/what/todo.md` TODO-001/008/009/015 | WHAT-014/015/016；HOW §1.4 |
| `archive/docs/what/review.md` REVIEW-016 | WHAT-014/016 |
| `archive/docs/what/execution.md` EXEC-004/028/031 | WHAT-004/010/011/016 |
| `archive/docs/what/glory.md` GLORY-004/006/072/074 | WHAT-008/009/015/016 |
| `archive/docs/why/companion.md` | WHY §4.2/4.4；WHAT-012 证据 |
| `archive/docs/how/companion.md` / `archive/docs/shape/companion.md` | HOW §1.1/1.3/1.4 |
| `archive/requirements-design/21-work-record.md` | 全部 WHAT 的 owner 裁决（OWNS 表） |
| `archive/requirements-design/13-context-continuity.md` | 边界裁决（DOES NOT OWN） |
| `archive/requirements-design/COVERAGE.md` COMPANION-003/014/015、REVIEW-016、TODO-001/008、GLORY-004 行 | WHAT 命题归属 |

### 4.2 弃权（GARBAGE / 明确不归本包）

- **GLORY-016/017/023/024 的「Birth/Labor floor」「Activation 前置」措辞**（COVERAGE GARBAGE 裁决）：
  措辞退役；Opening protection 语义由本包 WORK-RECORD-015 保留，旧 stage 词不升级为命题。
- **`Terminal` 完成标记的捕获机制**：归 semantic-trace（TerminalOutputCaptured 事实）；
  本包只声明「不是 LWR 段」。
- **`exact constants`（如 `InvocationStartCursor` 的具体取值算法）**：是 HOW；命题是
  bounded 语义（WORK-RECORD-016）。
- **`SyncDelegatePromptRequest { Charge; ProviderPrompt }`**：delegation 的 prompt 结构，
  不是 report DTO；本包不拥有（WORK-RECORD-012 边界里已注明）。

## 5. 依赖理由（DEPENDS ON）

- `semantic-trace`：record 的三段全部来自 XTrace 区间（Opening 用 `forOpening`，Recent 用
  `forWorkRecord` + `sliceFrom`）——record 是 trace 的物化，不是第二事实源。
- `context-compression`：Chronicle 的存在依赖 Y frames 的覆盖表示（frame 是压缩产物）。
- `participant-horizon`：record 作为跨 participant 传递物，其内容准入由 horizon 保证
  （INDEX.md 骨架：`work-record → semantic-trace, context-compression, participant-horizon`）。
