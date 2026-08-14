# knowledge-reuse

## 一句话 WHY

> 已花成本建立的 repository knowledge 应能复用，但旧答案永远不是当前正确性的证明；reuse 是 best-effort cache/hint，不是知识数据库 authority。Casebook 让 Inspector 的调查成果可 fetch、可按当前 worktree 重放、无变化时复用——同时绝不把 freshness 冒充 correctness。

## 阅读顺序

1. `WHY.md` — 为什么这个包必须独立存在；RED 长什么样。
2. `WHAT.md` — 唯一 normative 合同：12 条编号命题（`KNOWLEDGE-REUSE-001..012`）。
3. `HOW.md` — 实现模型（Casebook 类型 / capture / replay / workflow / Bookkeeper）与约束；含历史与弃权。
4. `PROOF.md` — 每条命题的可执行落点表；SPLIT@cutover 计划；semantic anchor 归属。

## WHAT 概览

| ID | 命题（压缩） |
|---|---|
| `KNOWLEDGE-REUSE-001` | Case 是 best-effort semantic cache 单元：Q+A+可重放 observations；不建知识数据库、无 commit history、不改 subject worktree。 |
| `KNOWLEDGE-REUSE-002` | Q 逐字 = 完整 Inspector initial prompt；A 逐字 = 实际 ToolResult body；observations 是该答案依据的可重放证据。 |
| `KNOWLEDGE-REUSE-003` | observation capture 是 typed 的：从工具执行捕获，不从 transcript 推断；捕获不完整不阻止归档。 |
| `KNOWLEDGE-REUSE-004` | fetch 只接受 shelfmark；先对当前 worktree 重放；no-delta → exact A + freshness hint；delta → refresh 或旧 A。 |
| `KNOWLEDGE-REUSE-005` | freshness ≠ correctness：replay/merge 标量/物理顺序不证明答案正确；维护失败 ≠ fetch 失败。 |
| `KNOWLEDGE-REUSE-006` | Bookkeeper 契约：CaseRefresh/CaseFinalize；`js-bookkeeper` 单程序原子变换；setQuestion/setAnswer 各至多一次；zero mutation 合法；无 filesystem capability；`edit-qa` 非法。 |
| `KNOWLEDGE-REUSE-007` | Case 的 durable authority = 统一 EventStore（InspectorCase* events + fold + PayloadRef）；禁 feature ref/LWW/pin/hook。 |
| `KNOWLEDGE-REUSE-008` | LRU 有界：淘汰 = `InspectorCaseEvicted` tombstone 事件；last_access 派生不独立 merge。 |
| `KNOWLEDGE-REUSE-009` | feature opt-in 双门：marker absent → schema 无 fetch + execution 拒绝 + 无 index/archive/Bookkeeper；未启用 repository 行为不变。 |
| `KNOWLEDGE-REUSE-010` | lifecycle：非复用 terminal → archive；复用 ReuseScope close → exactly one finalize；禁 per-return/idle/timer finalize；unexpected SessionDeleted 仅 cleanup。 |
| `KNOWLEDGE-REUSE-011` | 并发：同 Case 合法 fork 显式 DomainConflict；禁 (revision, wall_clock) LWW / timestamp 裁决；same-worktree fetch single-flight。 |
| `KNOWLEDGE-REUSE-012` | 低信任 index：只暴露 `{ shelfmark, canonical question }`；shelfmark 是稳定公开 locator 非 session identity；epoch 字节稳定。 |

## HOW 概览

- **类型**：`src/Wanxiangshu/Domain/Casebook.fs`（`Observation` / `Case` / `CasebookEvent` / `ReplayResult` / `ObservationIdentity` / `Observations.normalize` / `CasebookProjection.fold`）。
- **capture**：`src/Wanxiangshu/Infrastructure/CasebookCapture.fs`（read/glob/grep typed capture + executor 命令识别）。
- **replay**：`src/Wanxiangshu/Infrastructure/CasebookReplay.fs`（`replayAll` 对当前 worktree 只读重放）。
- **workflow**：`src/Wanxiangshu/Infrastructure/{CasebookWorkflow,CasebookLifecycle,CasebookSessionDraft,CasebookIndex,CasebookStore,CasebookBookkeeper,BookkeeperStaging,BookkeeperRuntime}.fs`。
- **Bookkeeper**：`js-bookkeeper(program)` 工具面在 `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JsBookkeeperTool.fs`。
- 细节见 `HOW.md`。

## proof 概览

- 本包测试（MOVE 自 `tests/unit/casebook/`，14 文件）：capture / domain / store / index / fetch / lifecycle / bookkeeper-{mechanical,session,synthesis} / js-bookkeeper-tool / edit-qa-tool / g6-* / universal-loop。
- 单跑：`node --test requirements/knowledge-reuse/tests/<file>`（设 `WANXIANGSHU_PROVIDER_LANGUAGE=en`）。全套：`node requirements/verification-system/tests/run.mjs`。

## 边界（DOES NOT OWN）

- durable store substrate（统一 EventStore）→ `durable-events`；general replica convergence（set union / DomainConflict 物理层）→ `durable-convergence`。
- 当前 repository fact acquisition 与 warm-start/semantic-search → `repository-investigation`（本包只消费 replay 结果做 freshness hint）。
- Bookkeeper 当前 Persona/tool/programming HOW（`fast-bookkeeper` 机器 id、Clerk/Curator Persona、`js-bookkeeper` 具体实现）→ HOW（`session-ontology`/`participant-identity` 交叉）。
- Case 内容语义（Q/A 该不该这样答）→ 无 owner（cache 内容不是规范）；「复用规则」才是本包。

## DEPENDS ON

`repository-investigation`、`durable-events`、`durable-convergence`（逐条理由见 `HOW.md` §依赖）。
