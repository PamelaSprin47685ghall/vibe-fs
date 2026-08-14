# Casebook — 证明义务

**G6 Product Exit DONE**（2026-08-12 Amendment：唯一 Long Stroke = mock LLM + 本机 OpenCode，即正确 Host）。digest synthesizer 仍 gone。G2 `promptModel` 未删除（非 G6 所有权）。不另开第二条 e2e、不抬超时。

## 证明义务清单

| 义务 | 证明方式 |
|---|---|
| **Feature gating（§60）** | marker absent：无 fetch schema（provider 门）+ ToolRegistry execute fetch 拒绝（execution 门）+ 无 index + 无 Bookkeeper config 要求 + 无 archive / 无 InspectorCase* append；双门独立测试 |
| **Q/A 逐字性（§61）** | 新 Case Q 逐字等于完整 Inspector initial prompt（不摘要）；A 逐字等于实际 ToolResult body；oversized 先 ToolResultBound 再 Captured payload；Bookkeeper 可改 Q/A、连续多次 `js-bookkeeper`、零 edit idle 合法、`js-bookkeeper` 不能写第三文件、最终 A 仍满足 ToolResultBound |
| **Observation capture（§62）** | read full/range、glob zero/multi、grep zero/multi、文件 deletion/create/rename 导致 glob/grep 变化；capture 不完整不阻止归档（original 成功 + Case Captured + 少一次检测机会） |
| **Executor parser（§63）** | 正例：`cat file` / `cat -n` / `head -n 30` / `tail -100` / `sed -n '20,80p'` / `cat file \| grep bar`；负例安全跳过不报错：`cat "$(...)"` / `sh -c` / `bash -c` / 命令替换 / 复杂 pipeline |
| **Replay freshness** | no-delta → exact A；delta → stale A + refresh 意图；freshness 非正确性证明 |
| **fetch 免费热路径** | index/fetch/replay 低成本；same-worktree single-flight |
| **CasebookProjection** | Captured/Refreshed/Accessed/Evicted fold 正确性；同 Case DomainConflict 表达；禁止 LWW（gate） |
| **LRU** | prune 按 projected last_access；Evicted tombstone 生效；单 Case 超界 |
| **Lifecycle** | 复用 scope：ReuseScope close → exactly one CaseFinalize → retire/release；禁止 per-return/idle/timer 等 finalize；unexpected SessionDeleted 仅 cleanup。实现：`src/Wanxiangshu/Infrastructure/CasebookLifecycle.fs` `tryFinalizeInspector`：draft Q/A → `BookkeeperRuntime.runTransaction` `CaseFinalize`（full transcript）→ `CasebookWorkflow.finalizeCase`；unexpected delete = `cleanupInspector` 零 EventStore 写。Host：`tests/e2e/support/long-stroke-oracles.mjs` `assertG6BookkeeperFinalize`（CaseFinalize envelope + `js-bookkeeper` + `InspectorCaseCaptured`）；SyncDelegate owner = `g2-inspector-owner`（与 Inquiry 同 CE，不另开专名 stroke）。unit：`tests/unit/casebook/g6-inspector-tool-finalize-fetch.test.mjs`、`lifecycle-wiring.test.mjs`、`universal-loop.test.mjs`。Host-path：`HostSessionDeletion`（经 `HostSignalBootstrap` SessionDeleted 路由）await finalize。详见下表 G6-F。 |
| **Bookkeeper** | CaseRefresh 0..N `js-bookkeeper` 单事务；stability verify；maintenance failure ≠ fetch failure（失败返回旧 A）。Persona = Clerk/Curator；机器身份 `fast-bookkeeper`/`deep-bookkeeper`（不复用 Inspector self-model）。实现：`BookkeeperRuntime` CreateChildSession + `js-bookkeeper` only + staging；旧名 `edit-qa` 非法。`CasebookBookkeeper.refreshStale`：CaseRefresh + freeze → transaction → stability verify → `InspectorCaseRefreshed`；digest synthesizer **gone**。Host：Long Stroke mock-LLM `js-bookkeeper`。unit：`bookkeeper-session` / `bookkeeper-mechanical` / `bookkeeper-synthesis` / `universal-loop`。详见下表 G6-E。 |
| **Storage** | Case 事实只经统一 EventStore；无 feature ref / LWW / pin / hook（unified-store-gate 延续） |
| **Universal G6-G** | SyncDelegate owner → same reusable Inspector → multiple questions → no Student/Teacher/QA/SKILL；ReuseScope close → one CaseFinalize → one Case；new Session → fetch → Case。Host：Long Stroke `g2-inspector-owner` Q1→Q3 + recursive delete → CaseFinalize → later Coder cold fetch（`G6_FETCH_CANARY_PROMPT` / `tests/e2e/entry.test.mjs`）。unit：`g6-inspector-tool-finalize-fetch.test.mjs`。详见下表 G6-G。**G6 Product Exit DONE**。 |

## G6-E / G6-F / G6-G

2026-08-12 Amendment：mock-LLM Long Stroke = 正确 Host。CaseRefresh 留 unit（不另开 e2e）。SyncDelegate owner 路径 = G6-G（不另开 Inquiry 专名 stroke）。

| Gate | Exit 证据 |
|---|---|
| **G6-E** CaseRefresh | unit：`refreshStale` freeze → CaseRefresh tx → stability verify → `InspectorCaseRefreshed`（`bookkeeper-session` / `bookkeeper-mechanical` / `bookkeeper-synthesis` / `universal-loop`）。digest synthesizer **gone**。工具名 `js-bookkeeper`（旧 `edit-qa` 非法）。 |
| **G6-F** CaseFinalize | 实现：`tryFinalizeInspector` → `CaseFinalize`（full transcript）→ `finalizeCase` once；unexpected = `cleanupInspector` 零写。Host：`assertG6BookkeeperFinalize`。unit：`g6-inspector-tool-finalize-fetch`、`lifecycle-wiring`。Host-path：`HostSessionDeletion`（经 Bootstrap 路由）await finalize。 |
| **G6-G** Universal close | Host：`g2-inspector-owner` Q1→Q3 + recursive delete → CaseFinalize → later Coder cold fetch（`G6_FETCH_CANARY_PROMPT`）。unit inspector-tool path。无 Student/Teacher/QA/SKILL（G3 ratchet）。 |

## 门禁

```text
npm run format-build-test
```

唯一 e2e = Long Stroke（mock LLM + 本机 OpenCode）；G6 不得另开第二条、不得提升超时。Long Stroke 时间边界（G4R）持续生效；Casebook 相关回归走 unit + Long Stroke 受影响路径。
