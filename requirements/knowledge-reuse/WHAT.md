# WHAT — knowledge-reuse 的唯一 normative 合同

> 命题 = 当前世界必须同时成立的事实。每条命题有测试落点（见 `PROOF.md`）。
> 边界（DOES NOT OWN）写在各条「边界」；更完整的弃权记录在 `HOW.md` §历史与弃权。

## KNOWLEDGE-REUSE-001 — Case 是 best-effort semantic cache 单元

**规范陈述**：Inspector Casebook 是 best-effort semantic cache：每个 Case 保存 Q&A 与可重放的 repository observations；后续 Inspector 可 fetch 并按当前 worktree 重放。不建立知识数据库，不引入 commit history / feature Git history，不保证历史 Q/A 可追溯为产品 API，不用 timestamp 判断 freshness 或 merge winner，不改变 subject worktree。

**含义/动机**：Casebook 是 availability-and-reuse-over-freshness 的缓存，不是证明系统。把它当知识数据库 = 给「旧答案权威性」开非法入口；把它当只读复用 = 复用成本与现状解耦。

**边界**：Case 内容（Q/A 具体值）无 owner（缓存内容不是规范）；「复用规则」才是本包。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-001 行。

## KNOWLEDGE-REUSE-002 — Case 内容：Q/A 逐字 + observations

**规范陈述**：Case 的 Q 逐字等于完整 Inspector initial prompt（不经过摘要）；A 逐字等于实际 Inspector ToolResult body（oversized 先走 ToolResultBound，再作为 Captured payload）；Observations 是该答案依据的、可重放的 repository observations（能捕获多少捕获多少，缺失允许）。Bookkeeper 可改 Q、可改 A、可连续多次 `js-bookkeeper`；最终 A 仍满足 ToolResultBound。

**含义/动机**：逐字性让 fetch 返回的 A 与当初呈现的一致——摘要会引入「第二作者」；observations 缺失只减少未来变化检测机会，不阻止归档。

**边界**：ToolResultBound 的 bound 语义本身 → `host-boundary`（ARCH-012 交叉）。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-002 行。

## KNOWLEDGE-REUSE-003 — observation capture 是 typed 的

**规范陈述**：Observation 从工具执行的 typed 结果捕获（read full/range → `FileRead`，glob → `GlobResult`，grep → `GrepResult`），**从不从 transcript 文本推断**。捕获不完整（如命令无法识别）不阻止归档：original Inspector 成功、Case 照常 Captured、缺失的 observation 只是未来少一次变化检测机会。阅读类命令（`cat`/`head`/`tail`/`sed` 单文件形式）识别为 observation；命令替换、`sh -c`、`bash -c`、无法确定读取目标的复杂 pipeline 安全跳过且不报错。同路径同内容观察经 `ObservationIdentity` 去重并规范化排序，保证同一证据折叠到同一 Case 字节。

**含义/动机**：文本推断在重放时不可靠、不可重放；typed capture 是「同一 observation 可再次验证」的唯一方式（历史 why/casebook 被拒方案：从 transcript 推断）。

**边界**：观察本身如何被工具产生（read/glob/grep 的语义）→ `repository-programming`/`repository-investigation` 交叉；本命题只管「Case 里的 observation 从哪来、如何规范化」。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-003 行。

## KNOWLEDGE-REUSE-004 — fetch 语义：shelfmark + 先重放

**规范陈述**：`fetch(shelfmark)` 只接受 provider-visible shelfmark，不暴露 durable session identity。它先针对当前 worktree 重放 observations：未检测到变化 → 返回 exact canonical A，并说明未发现 evidence 变化（no-delta 只是 freshness hint，不构成正确性证明）；检测到变化 → 进入 Bookkeeper refresh，成功则返回 revised canonical A，失败则返回旧 A 并明确它是 older account。fetch 对 Inspector 免费（低开销热路径）；重放只读，绝不写 subject worktree。

**含义/动机**：fetch 的价值在于「不用重新调查」，代价是「可能过时」；重放把过时风险显式化——模型知道这是 hint 还是 current fact。

**边界**：warm-start/semantic search 的低信任提示 → `repository-investigation`；本命题只管 Case fetch 路径。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-004 行。

## KNOWLEDGE-REUSE-005 — freshness ≠ correctness proof

**规范陈述**：任何 replay 结果、merge 标量或 EventStore 物理顺序都不证明答案正确（no-delta 只是 hint）。维护失败 ≠ fetch 失败：Bookkeeper 失败时保留旧 Case、返回旧 A——**允许过时是预期产品语义**。

**含义/动机**：把 freshness 当 correctness = 把 cache 当 truth。允许过时是显式产品决策：宁可给旧的诚实答案，也不伪造「已验证」。

**边界**：「什么构成当前事实」→ `repository-investigation`；「replay 本身是否可靠」由 capture/replay 实现决定（HOW）。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-005 行。

## KNOWLEDGE-REUSE-006 — Bookkeeper 契约与取证边界

**规范陈述**：私有 Bookkeeper Agent 提供两个 request contract：`CaseRefresh`（changed evidence → `js-bookkeeper`*（一个 provider transaction 内 0..N 次）→ stability verify → `InspectorCaseRefreshed`）与 `CaseFinalize`（ReuseScope close → freeze draft → exactly one finalize → `InspectorCaseCaptured` → retire/release）。工具唯一：`js-bookkeeper(program)`——一次程序 = 一次原子 staged 变换；`setQuestion`/`setAnswer` 各至多一次；zero mutation 合法；**无 filesystem capability**（不得回 repository 再取证）。旧名 `edit-qa` 非法、无 alias。不新建 LearningCompiler / CaseSynthesizer / StudentReplacement。

**含义/动机**：Bookkeeper 是 maintenance participant，不是第二 Inspector。它只能重塑**已供给证据上**的 staged Case；「回 repository 取证」会把维护变成新的调查，破坏 evidence-supplied 边界（历史 why/casebook：拒借用 Scout/Investigator 自我模型）。

**边界**：Bookkeeper 的 Persona（Clerk/Curator）、机器身份（`fast-bookkeeper`/`deep-bookkeeper`）、session 形态（InternalLeaf + Attached）→ HOW + `participant-identity`/`session-ontology` 交叉；「无取证权 + 原子 staged 变换」才是本命题。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-006 行。

## KNOWLEDGE-REUSE-007 — Case 的 durable authority = 统一 EventStore

**规范陈述**：Case durable authority 是统一 EventStore（`IEventStore`）：`InspectorCaseCaptured` / `InspectorCaseRefreshed` / `InspectorCaseAccessed` / `InspectorCaseEvicted` 事件 + `CasebookProjection` fold；大正文 = `PayloadRef` → store payloads。Journal / 诊断不得复制 Case bodies。禁止 feature ref / LWW / pin / Casebook hook / feature tree materialization as authority。物理 CAS / converge / dumb remote = Persist + GitGateway。Case 动态数据只经 `refs/wanxiang/store`，不进入 worktree。

**含义/动机**：Casebook 不拥有独立 durable store——否则无法共享 Persist 的 merge/CAS/恢复。feature store 的每一次出现都是未来不可收敛的分叉（`unified-store-gate` 延续）。

**边界**：EventStore 物理层 → `durable-events`；dumb-remote 同步 → `durable-convergence`/`change-integration` 交叉。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-007 行。

## KNOWLEDGE-REUSE-008 — LRU 有界性：淘汰是事件

**规范陈述**：Casebook 使用有限 LRU：淘汰通过 append `InspectorCaseEvicted` 表达，长期无人使用的条目退出 live projection；单 Case 超界按 prune key 处理。last_access 由 `InspectorCaseAccessed` 投影派生（monotonic counter，不是 wall clock），不是独立 merge 文档；淘汰 tombstone 也是事件。

**含义/动机**：有界性是 cache 的定义性质；淘汰必须可重放（事件）而非瞬时内存操作。last_access 派生避免第二个「访问时间」真相与投影竞争。

**边界**：具体 capacity / prune key 权重 → HOW 常数。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-008 行。

## KNOWLEDGE-REUSE-009 — feature opt-in 双门

**规范陈述**：marker absent → Inspector 无 fetch schema（provider 门）+ ToolRegistry execute fetch 也拒绝（execution 门）+ 无 Casebook index + 无 Bookkeeper config requirement + 无 archive / 无 InspectorCase* append——Casebook surface 全关。未启用 Casebook 的 repository 行为保持不变。

**含义/动机**：只隐藏 schema 不够——模型可伪造调用名；只拒绝 execution 不够——provider 会看到不存在的工具。双门独立测试（历史 how/casebook Feature gating）。opt-in（而非 opt-out）保证未启用仓库与现状逐字节一致。

**边界**：marker 目录名（`.wanxiang/casebook`）与检测机制 → HOW。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-009 行。

## KNOWLEDGE-REUSE-010 — lifecycle：exactly-one finalize

**规范陈述**：非复用 Inspector scope：terminal → archive（`InspectorCaseCaptured`）。复用 Inspector scope：调用期间只 capture，不逐次 finalize；ReuseScope close → exactly one CaseFinalize → retire/release reusable Inspector。禁止每个 SyncDelegate invocation finalize / 每个 owner turn finalize / idle finalize / timer finalize / token 阈值 finalize。unexpected SessionDeleted → 仅 cleanup，不 reconstruct + synthesize（Casebook 是 cache，不值得 durable pending-finalize workflow）。

**含义/动机**：逐调用 finalize 把一次复用调查碎片化成多个半 Case；exactly-one 让一个 ReuseScope 对应一个完整 Q/A 单元。崩溃/删除不值得为 cache 启动恢复工作流。

**边界**：ReuseScope 生命周期本身 → `managed-session-lifecycle`/`session-ontology` 交叉；本命题是「何时 finalize」的 Casebook 语义。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-010 行。

## KNOWLEDGE-REUSE-011 — 并发：显式 DomainConflict，禁 LWW

**规范陈述**：同一 Case 合法并发 fork 由投影表达为 **DomainConflict**，经后续 resolution / refresh / evict events 收敛。replica 收敛 = EventStore set union。禁止 `(revision, wall_clock)` LWW、同值 LWW、timestamp 同值裁决。不同 Case 并发互不干扰；同一 Case / 同一 worktree 并发由 same-worktree fetch single-flight 串行化。

**含义/动机**：物理层只做 union，业务正确性不由 merge 证明；「哪个分支更正确」不是 merge 能回答的——显式 conflict 让后续 resolution/refresh 有机会裁决。静默 LWW = 丢分支。

**边界**：general replica convergence / set union / DomainConflict 物理机制 → `durable-convergence`（本包消费）；本命题是「Case 对象必须显式 conflict、不 LWW」的对象语义。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-011 行。

## KNOWLEDGE-REUSE-012 — 低信任 index：只暴露 shelfmark + canonical Q

**规范陈述**：CasebookIndexSnapshot 是低信任数据：provider 只看到 `{ shelfmark, canonical question }`；shelfmark 是稳定公开 locator，内部再解析到 durable Case identity，绝不把 session/status/freshness 机器字段带到 provider。相同 provider-visible index 在同一 epoch 字节稳定；invalidate 或可见 index 变化推进 epoch；probe promotion 继承 frozen index。

**含义/动机**：index 是 provider 的导航面，不是内部状态投影（ARCH-014 horizon）；机器字段泄漏会让 provider 获得不该有的内部知识。epoch 字节稳定保证同一次呈现内 index 不跳变。

**边界**：epoch freeze 机制本身 → `prefix-stability`/`provider-projection` 交叉；「可见面只有 shelfmark + canonical Q」才是本命题。

**证据**：→ `PROOF.md` KNOWLEDGE-REUSE-012 行。
