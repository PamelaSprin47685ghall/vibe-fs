# effect-accounting — WHAT

## EFFECT-ACCOUNTING-001: Requested/Claimed 与 Accepted/Created/Published 分型

每个外部副作用必须由两类强类型持久化事实成对表达：请求意图（Request/Claim）与物理确认（Accepted/Created/Published）。它们属于不同的类型用例，严禁使用单一布尔字段或状态枚举概括。早期通用的未分型副作用联合体一律在解码阶段拒绝。

## EFFECT-ACCOUNTING-002: Requested-only 代表 outcome unknown

仅存在 Request/Claim 事实而缺失 Accepted 事实时，副作用处于“结局未知”状态。该状态既不代表副作用未发生（禁止当成未发生而盲目重发），亦不代表副作用已成功（禁止跳过核对直接推进后续阶段）。当修复预算耗尽时，必须转为明确的终局失败以终止挂起，禁止无限期停留在待决状态。

## EFFECT-ACCOUNTING-003: durable intent 先于权威内存状态更新

外部副作用的意图事实必须先于权威内存状态的变更，且先于底层物理操作的执行（例如先追加 `WorktreeCreateRequested` 事实再执行物理工作区创建，先追加 `TodoWritePrepared` 再调用提供者）。严禁先执行物理动作后补记账。

## EFFECT-ACCOUNTING-004: Accepted 不折回 Requested 且重复 acceptance 幂等

已确认的事实（Accepted/Created/Published）绝对禁止因重放或重试而折回 Requested 状态。收到重复的确认事件时必须保持幂等，不得改变已确认的业务状态或产生二次副作用。

## EFFECT-ACCOUNTING-005: reconciliation 先查物理 effect identity 且禁止盲重试

在崩溃或中断后处理仅有 Requested 的副作用时，必须首先核对外部物理实体的存在性证据（如工作区是否存在、分支引用是否已推进、提供者回执是否已返回）。只有在证实物理副作用尚未发生且领域合同允许幂等重试的前提下才可发起重试；否则必须保持待决挂起。

## EFFECT-ACCOUNTING-006: outcome-unknown 显式分型不假装 committed

当事实追加或副作用执行发生不确定异常时，系统必须以显式的未决分型（如 `CommitUnknown`、`WriteUnknown` 或 Pending）表达，严禁静默假装提交成功，亦严禁将未收到返回直接等同于未发生。

## EFFECT-ACCOUNTING-007: aborted 不等于 terminal

控制面的取消操作（aborted）不是业务执行的终态。Agent 的合法终态仅包括 `Completed`、`Failed` 与 `Abandoned`。系统严禁将中止信号洗白为完成终态，亦不得将取消误判为提供者崩溃。

## EFFECT-ACCOUNTING-008: typed 效果家族实例

系统中的具体副作用均遵循成对的类型化事实约定：工作区创建（`WorktreeCreateRequested` / `WorktreeCreated`）、分支发布（`PublishClaimed` / `Published`）、结构化日志记录（`BloggerRequestMaterialized` / `EntryCommitted`）以及待办写入（`TodoWritePrepared` / `TodoWriteAccepted`）。

## EFFECT-ACCOUNTING-009: PublishClaimed 三分支固定判定顺序

发布副作用的核对必须按固定顺序检查物理分支引用：
1. 目标分支已指向候选提交 → 证明发布已物理发生，补写 `Published` 事实；
2. 目标分支仍处于预期头部 → 证明目标未变，执行发布提交；
3. 目标分支已被他人推进 → 证明凭证已作废，重新发起变基与评审。
无法读取目标分支时必须 fail-closed 阻断，严禁猜测。

## EFFECT-ACCOUNTING-010: 历史通用 DurableEffect union 明确拒绝

历史遗留的通用 `DurableEffectRequested` 与 `DurableEffectAccepted` 标记必须被显式拒绝并给出迁移指引，禁止在当前系统中混合双读或作为有效词汇表使用。

## EFFECT-ACCOUNTING-011: TodoWriteAccepted 必须精确指名 Prepared

`TodoWriteAccepted` 必须精确包含其对应的 `TodoWritePrepared` 事件引用与载荷哈希校验。引用缺失或摘要失配必须作为状态破坏拒绝接受。确认事实生效后当前待办义务立即更新，后续评审不得回滚已确认的检查点。

## EFFECT-ACCOUNTING-012: 先证后重试的实例律

所有业务副作用的重试门禁均须严格依凭证据驱动：必须在上一阶段确认事实完成后方可准备下一阶段调用；发布 Claim 必须基于已完成变基与双重评审的不可变见证事实，严禁脱离证据凭空发起。
