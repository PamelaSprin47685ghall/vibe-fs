# Casebook — 证明义务

## 证明义务清单

| 义务 | 证明方式 |
|---|---|
| **Feature gating（§60）** | marker absent：无 fetch schema（provider 门）+ ToolRegistry execute fetch 拒绝（execution 门）+ 无 index + 无 Bookkeeper config 要求 + 无 archive / 无 InspectorCase* append；双门独立测试 |
| **Q/A 逐字性（§61）** | 新 Case Q 逐字等于完整 Inspector initial prompt（不摘要）；A 逐字等于实际 ToolResult body；oversized 先 ToolResultBound 再 Captured payload；Bookkeeper 可改 Q/A、连续多次 edit-qa、零 edit idle 合法、edit-qa 不能写第三文件、最终 A 仍满足 ToolResultBound |
| **Observation capture（§62）** | read full/range、glob zero/multi、grep zero/multi、文件 deletion/create/rename 导致 glob/grep 变化；capture 不完整不阻止归档（original 成功 + Case Captured + 少一次检测机会） |
| **Executor parser（§63）** | 正例：`cat file` / `cat -n` / `head -n 30` / `tail -100` / `sed -n '20,80p'` / `cat file \| grep bar`；负例安全跳过不报错：`cat "$(...)"` / `sh -c` / `bash -c` / 命令替换 / 复杂 pipeline |
| **Replay freshness** | no-delta → exact A；delta → stale A + refresh 意图；freshness 非正确性证明 |
| **fetch 免费热路径** | index/fetch/replay 低成本；same-worktree single-flight |
| **CasebookProjection** | Captured/Refreshed/Accessed/Evicted fold 正确性；同 Case DomainConflict 表达；禁止 LWW（gate） |
| **LRU** | prune 按 projected last_access；Evicted tombstone 生效；单 Case 超界 |
| **Lifecycle** | 复用 scope：ReuseScope close → exactly one CaseFinalize → retire/release；禁止 per-return/idle/timer 等 finalize；unexpected SessionDeleted 仅 cleanup |
| **Bookkeeper** | CaseRefresh 0..N edit-qa 单事务；stability verify；maintenance failure ≠ fetch failure（失败返回旧 A） |
| **Storage** | Case 事实只经统一 EventStore；无 feature ref / LWW / pin / hook（unified-store-gate 延续） |
| **Universal G6-G** | Meditator → same reusable Inspector → multiple questions → no Student/Teacher/QA/SKILL；ReuseScope close → one CaseFinalize → one Case；new Session → new Inspector → fetch → Case |

## 门禁

```text
node scripts/checks/spec.mjs
node scripts/checks/architecture.mjs
node scripts/checks/unified-store-gate.mjs
node scripts/checks/js-surface-gate.mjs
npm run build
node tests/unit/run.mjs
node tests/integration/run.mjs
npm run test:e2e（Long Stroke 单一入口；不得因 G6 提升超时）
npm run check
环境允许时：npm run check:release
```

Long Stroke 时间边界（G4R）持续生效；Casebook 相关回归走 unit + Long Stroke 受影响路径。
