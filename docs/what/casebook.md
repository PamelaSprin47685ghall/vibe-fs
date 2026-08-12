# Casebook — 可观察语义

Clause 前缀 `CASE-`。本页只冻结 observable semantics；内部模块名见 how。

## CASE-001 Casebook 定位

Inspector Casebook 是 best-effort semantic cache：每个 Inspector Session 保存当前 Q&A 与可重放的 repository observations；后续 Inspector 可 fetch 并按当前 worktree 重放。不建立知识数据库，不引入 commit history / feature Git history，不保证历史 Q/A 可追溯为产品 API，不用 timestamp 判断 freshness 或 merge winner，不改变 subject worktree。完全 opt-in：repository 存在指定 marker directory 才启用。

## CASE-002 Case 内容

- Q：逐字等于完整 Inspector initial prompt（不经过摘要）。
- A：逐字等于实际 Inspector ToolResult body（oversized 先走现有 ToolResultBound，再作为 Captured payload）。
- Observations：该答案依据的、可重放的 repository observations（能捕获多少捕获多少，缺失允许）。
- Bookkeeper 可改 Q、可改 A、可连续多次 `js-bookkeeper`；零 edit idle 合法；`js-bookkeeper` 不能写第三个文件；最终 A 仍满足 ToolResultBound。

## CASE-003 Observation capture

Observation 从工具执行的 typed 结果捕获，从不从 transcript 文本推断。捕获不完整（如命令无法识别）不阻止归档：original Inspector 成功、Case 照常 Captured、缺失的 observation 只是未来少一次变化检测机会。阅读类命令：`cat` / `head` / `tail` / `sed` 单文件形式识别为 observation；命令替换、`sh -c`、`bash -c`、无法确定读取目标的复杂 pipeline 安全跳过且不报错。

## CASE-004 fetch 语义

`fetch(session_id)` 不直接信任旧答案：先针对当前 worktree 重放 observations。未检测到变化 → 直接复用旧 A（no-delta 只是 freshness hint，不构成正确性证明）；检测到变化 → 旧 A 标记 stale，进入 refresh 意图。fetch 对 Inspector 免费（低开销热路径）。

## CASE-005 freshness 不是正确性证明

任何 replay 结果、merge 标量或 EventStore 物理顺序都不证明答案正确。维护失败 ≠ fetch 失败：Bookkeeper 失败时保留旧 Case、返回旧 A（允许过时是预期产品语义）。

## CASE-006 Bookkeeper

私有 Bookkeeper Agent 提供两个 request contract：`CaseRefresh`（changed evidence → `js-bookkeeper`*（一个 provider transaction 内 0..N 次）→ stability verify → InspectorCaseRefreshed）与 `CaseFinalize`（ReuseScope close → freeze draft → exactly one finalize → InspectorCaseCaptured → retire/release reusable Inspector）。不新建 LearningCompiler / CaseSynthesizer / StudentReplacement。

Bookkeeper 不可见（AGENT-008）；机器身份 `fast-bookkeeper` / `deep-bookkeeper`（强制内部 pair，AGENT-002）；Persona = Clerk / Curator（AGENT-028）；可复用 inspector 模型绑定，**不**复用 Inspector self-model。feature disabled 时不要求 Bookkeeper config。

工具唯一：`js-bookkeeper(program)`。旧名 `edit-qa` 非法，无 alias。一次程序 = 一次原子 staged 变换；`setQuestion` / `setAnswer` 各至多一次；zero mutation 合法；无 filesystem capability。

## CASE-007 Storage

Case durable authority = 统一 EventStore（`IEventStore`）：`InspectorCaseCaptured` / `InspectorCaseRefreshed` / `InspectorCaseAccessed` / `InspectorCaseEvicted` 事件 + `CasebookProjection` fold；大正文 = `PayloadRef` → store payloads。Journal / 诊断不得复制 Case bodies。禁止 feature ref / LWW / pin / Casebook hook / feature tree materialization as authority。物理 CAS / converge / dumb remote = Persist + GitGateway。

## CASE-008 LRU 与有界性

Casebook 使用有限 LRU：淘汰通过 append `InspectorCaseEvicted` 表达，长期无人使用的条目退出 live projection；单 Case 超界按 prune key 处理。last_access 由 `InspectorCaseAccessed` 投影派生，不是独立 merge 文档；淘汰 tombstone 也是事件。

## CASE-009 Feature gating

marker absent → Inspector 无 fetch schema、ToolRegistry execute fetch 也拒绝（双门：provider schema + execution registry）、无 Casebook index、无 Bookkeeper config requirement、无 archive / 无 InspectorCase* append——Casebook surface 全关。未启用 Casebook 的 repository 行为保持不变。

## CASE-010 Lifecycle

非复用 Inspector scope：terminal → archive（InspectorCaseCaptured）。复用 Inspector scope：调用期间只 capture，不逐次 finalize；ReuseScope close → exactly one CaseFinalize → retire/release reusable Inspector。禁止每个 SyncDelegate invocation finalize / 每个 owner turn finalize / idle finalize / timer finalize / token 阈值 finalize。unexpected SessionDeleted → 仅 cleanup，不 reconstruct + synthesize（Casebook 是 cache，不值得 durable pending-finalize workflow）。

## CASE-011 并发与收敛

同一 Case 合法并发 fork 由投影表达为 DomainConflict，经后续 resolution / refresh / evict events 收敛。replica 收敛 = EventStore set union。禁止 `(revision, wall_clock)` LWW、同值 LWW、timestamp 同值裁决。不同 Case 并发互不干扰；同一 Case / 同一 worktree 并发由 same-worktree fetch single-flight 串行化。

## CASE-012 低信任数据

CasebookIndexSnapshot 是低信任数据：Q list containment 受控（fetch 只按 session_id 查找，index 不引入不可信路径）；同 epoch 字节稳定；Casebook 更新不制造新 epoch；probe promotion 继承 frozen index。
