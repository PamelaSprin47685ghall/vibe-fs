# semantic-trace — HOW（实现模型与约束；非 normative）

> 本文件解释「当前实现怎么满足 WHAT」，不是第二条规范。命题只在 `WHAT.md`。

## 1. 实现模型

### 1.1 类型层（`src/Wanxiangshu/Domain/XTrace.fs`）

```fsharp
type XTraceCursor = { Sequence: int64 }
type XTraceItem  = { Cursor: XTraceCursor; Provenance: string; Role: string; Part: SemanticPart }
type RecordCoverage = { IngestedThrough: XTraceCursor }
type PrefixCoverage = { HostEpochId; CutoffExclusive; CoveredPrefixDigest; CoverableFrameCount }
```

- `XTrace.nextCursor` / `isAfter` 提供严格单调；`sliceBetween`/`sliceFrom`/`head` 提供半开定位。
- `flatten` 是 SemanticMessage → 带 role 的 part 序列的唯一平铺（SEMANTIC-TRACE-007）。
- `forWorkRecord` 过滤 raw tool（LWR 用）；`forOpening` = identity（Opening 保留一切，SEMANTIC-TRACE-010）。
- `render` 永不输出 Provenance；assistant 正文不带 role 前缀；空列表渲染为空字符串。

### 1.2 durable 投影（`src/Wanxiangshu/Journal/XTraceProjection.fs`）

`XTraceProjectionState = { Opening: OpeningMaterial option; Parts: XTracePartRef list; Terminal: (BlobRef*BlobDigest) option }`

- `Parts` 存储 newest-first（replay cons O(1)），`parts` 恢复 oldest-first。
- 三个 fold 规则（PERSIST-010）：
  - `applyOpening`：同文本幂等；异文本 `OpeningAlreadyCaptured` 拒绝；
  - `applyPart`：cursor 必须严格大于 head；否则 `CursorNotAfterHead` 拒绝；
  - `applyTerminal`：同 ref+digest 幂等；不同 terminal 覆盖（subagent reuse 每 work unit 一个 terminal，私有完成标记）。
- `provenanceGeneration` 解析 `g:N/...`（reanchor 后），legacy `turn:N/part:M` → 0。
- `currentGenerationParts` 只取最新 generation，避免跨 reanchor 混用 Host turn 编号。

### 1.3 捕获链路（`src/Wanxiangshu/Application/Reconciliation/XTraceCapture.fs`）

- `semanticPart`：唯一的 `MessagePart → SemanticPart` mapper；`Activity` → `None`（丢弃）。
- `captureSources`：按 provenance `g:N/turn:M/part:P` 幂等 append（recorded 集合去重）。
- `captureSourcesStable`：按 `g:N/msg:<id>/part:P`（STRENGTH-008 stable insertion 前提）；
  legacy positional trace 只读、强制 Strength K0。
- `captureGeneration`：generation = `ReanchoredRuns` 集合大小，reanchor 后 +1。
- `captureOpening` / `captureTerminalText` / `captureLastWords`：Opening 与 Terminal 的捕获入口。

### 1.4 fold 接线（`src/Wanxiangshu/Journal/Fold.fs` + `ContextFactFold.fs`）

- XTrace 事实经 `Fold` 维护（durable-events substrate）。
- `ContextReanchored` 在 `ContextFactFold` 只更新 `PrefixEpoch` / `Blog` / `TipDelivery`，
  **不动 XTrace**（SEMANTIC-TRACE-009 的结构保证）。

## 2. 消费方（为什么其它包依赖本包）

| 消费方 | 消费什么 |
|---|---|
| `work-record` | `XTrace.forOpening` + `forWorkRecord` + `sliceFrom` 物化 LWR |
| `context-compression` | Blogger chunker 的 `SemanticCursor → XTrace cursor` 映射 |
| `prefix-stability` | `CoveredPrefixDigest` 的源语义投影 |
| `review-assurance` / `finality` | review frontier 的 canonical 证据源 |

## 3. 与 `durable-events` 的分工

- 本包不拥有：事件如何编码/落盘/拒绝。`PERSIST-010` 的拒绝规则类型
  （`XTraceFoldRejection`）定义在 `XTraceProjection.fs`，但「拒绝 = 启动失败 vs 幂等吸收」
  的 fold 语义归 `durable-events`（见 `fold-context-recovery.test.mjs` 的注释）。
- 本包拥有：capture 边界、cursor 语义、provenance、frontier/range 合同。

## 4. 已知非目标（HOW 层，不升级为命题）

- `XTracePartRef.Turn/PartIndex` 是 Host semantic 坐标，仅供 writer 把 BlogEntry 的
  SemanticCursor 映射回 XTrace cursor；XTrace cursor 本身独立于它们（`XTraceProjection.fs` 注释）。
- `supportsStableInsertion` 的存在（STRENGTH-008）是 Strength 优化 HOW；「Candidate 永不入迹」
  才是命题（SEMANTIC-TRACE-008）。
- `Provenance` 字符串格式（`g:N/turn:M/part:P` vs `g:N/msg:id/part:P`）是定位实现，可演进。

## 5. 历史与弃权

### 5.1 源 → 覆盖映射

| 源 | 信息落点 |
|---|---|
| 历史 HOST-005/006 | WHAT-001/002/003/004/009；WHY §1/§3 |
| 历史 COMPANION-003/007/008/012/014 | WHAT-001/005/006/007/009/010 |
| 历史 why/context（probe 失败不写事实） | WHAT-008；WHY §4.3 |
| 历史 why/strength + 历史 change（strength） | WHAT-008；WHY §4.1 |
| 历史 change（cursor-pair-hint）§12（prefix/idempotence scope） | 与 HOST-013 互斥 → 本包只记「XTrace 无 synthetic 正文」；主体归 prefix-stability（见该包 HOW §5） |
| 历史 change（cache）（HOST-013 anchored prefix） | 同上；anchor 语义归 prefix-stability |
| 历史 COVERAGE（HOST-005 / COMPANION-003/007/008 行） | WHAT-001..010 的 owner 裁决 |
| 历史 EVIDENCE（semantic-trace 行） | HOW §1 的实现路径 |

### 5.2 弃权（GARBAGE / 明确不归本包）

- **UI delta / usage / cost / timestamp 的「计量格式」**：HOST-005 只要求它们不进 XTrace；
  它们是否别处该记、怎么记，不是本包命题（journal 诊断有独立 owner）。
- **`TerminalOutputCaptured` 的「私有完成标记」语义**：terminal 不是 LWR 段、不经 Y ——
  这个事实归 `work-record`（WORK-RECORD-011 边界）；本包只拥有「terminal 是 XTrace 的
  第三事实且幂等捕获」。
- **Host compaction 的预防/收容两层机制**：`HostCompactionPolicy` 的 setting 清单与
  probe verdict 归 `context-compression`；本包只保留「XTrace 不删除」的结果。
- **`WorkActivated` / Birth-Labor 等 legacy 措辞**（GLORY-016/017/023/024 的 GARBAGE 裁决）：
  Opening protection 语义归 `work-record`；本包不重复。

## 6. 依赖理由（DEPENDS ON）

- `durable-events`：XTrace 的 append-only 与可重放必须由不可变事实 + 原子提交 + 确定性 fold
  提供（INDEX.md 87-edge 骨架的唯一 hard edge）。
