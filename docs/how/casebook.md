# Casebook — 已裁决算法与控制流

## 主流程

```text
Inspector 调用（复用或非复用 scope）
→ typed observation capture（read/glob/grep 工具执行）
→ scope terminal（非复用）或 ReuseScope close（复用）
→ freeze draft（Q 逐字 + A 逐字 + observations + snapshot refs）
→ exactly one finalize/archive provider transaction
→ Append InspectorCaseCaptured（大正文 PayloadRef → store payloads）
→ CasebookProjection fold 更新 index

后续 fetch(session_id)：
→ CasebookIndexSnapshot（当前 epoch 冻结）
→ lookup Case
→ 对当前 worktree replay observations（只读，不写）
→ no-delta → 返回 exact A（freshness hint，非正确性证明）
→ delta → 旧 A stale；启动 Bookkeeper CaseRefresh
→ Bookkeeper edit-qa*（0..N，一个 provider transaction）→ stability verify
→ Append InspectorCaseRefreshed → 返回新 A
→ 失败 → 保留旧 Case，返回旧 A
```

## CasebookProjection fold

输入：InspectorCaseCaptured / Refreshed / Accessed / Evicted 事件（因果序）。
- Captured：插入或替换 Case（Q/A/observations 来自 payload）。
- Refreshed：替换该 Case 的 A 与 observations。
- Accessed：更新 last_access（派生值，不参与 merge）。
- Evicted：从 live projection 移除。
- 同 Case 多 head（并发 fork）：投影为 DomainConflict；经 resolution/refresh/evict 收敛。
- 禁止：revision 排序、wall_clock 比较、timestamp 裁决。

## Observation normalization 与 replay

- normalize：路径按 repository containment 规范；同路径同内容观察去重（ObservationIdentity）。
- replay：对当前 worktree 重新执行捕获时的 typed 读取（文件存在性 + 内容 + glob/grep 结果集合）。
- classifyReplay：全部一致 → Fresh；任一缺失/变化 → Stale（含文件 create/delete/rename 导致的 glob/grep 变化）。
- 捕获不完整（executor 无法识别）→ 该 observation 不参与 replay（少一次变化检测机会，不阻止归档）。

## LRU prune

- prune key：projected last_access + Case 大小（payload 引用数）。
- 超界 → 选最久未访问 → Append InspectorCaseEvicted → fold 移除。
- 单 Case 超界：按 prune key 处理该 Case。
- 淘汰 tombstone 是事件；last_access 派生，不独立 merge。

## Feature gating（双门）

- marker 检测（repository 存在指定 marker directory）。
- 启用：fetch 工具 schema 注入 + ToolRegistry execute 允许 + archive 行为 + Bookkeeper config。
- 禁用：schema 无 fetch、execute 拒绝（fail-closed）、无 index、无 archive、无 InspectorCase* append。
- 两门独立测试（provider schema / execution registry），不能只隐藏 schema。

## Bookkeeper 生命周期

- CaseRefresh：changed evidence → Bookkeeper（InternalLeaf + Attached）→ edit-qa* → stability verify（replay 再次 Fresh）→ Refreshed。
- CaseFinalize：ReuseScope close → freeze draft → 一个 finalize 事务 → Captured → retire/release。
- 失败路径：maintenance failure ≠ fetch failure；返回旧 A。
- unexpected SessionDeleted：仅 cleanup，不 reconstruct。

## 并发

- 不同 Case：独立并行。
- 同一 Case / 同一 worktree：fetch single-flight 串行化。
- 不同 worktree 同一 Case：EventStore set union 收敛；DomainConflict 投影。
