# managed-chat-execution — HOW

## 架构机制

### 1. Durable projection

`ManagedChatExecutionProjection` 以 `(SessionId, PhysicalUserMessageId)` 分区，纯折叠 versioned facts：

```text
None
  └─ Accepted
       ├─ ProviderStarted ─ Terminal(disposition)
       └─ Terminal(disposition)        # pre-provider settlement
```

重复等值 fact 不改变投影；越级、冲突 identity、terminal 后启动及不同 terminal 竞争返回 typed conflict，绝不修补历史。事件 codec 与 upgrader 只处理 schema 演化，不读取时钟或环境。

### 2. Admission transaction

Host `chat.message` 先从 `interaction-authority` 取得 exact `AttemptExecutionProfile`。该 profile 必须携带上游以一次原子持久化产生的 durable root witness：root 的完整版本化 `ParticipantIdentityEvidence` 与对应 `AuthorityRootAccepted` 不可拆分、不可孤立提交。Host 校验该 witness 的 LogicalRunId 属于当前 durable run，再以 exact physical identity 与完整原子 profile 追加 `Accepted`；确认 durable 后才向 `execution-model-routing` 获取 exact capacity，建立 message-keyed binding，将选定 target 与 evidence 的逐字段只读投影送入 Host mutable message。任何组件不得缓存或从 agent/model/session 重新推导 Role、initial Tier、Persona、Peer。provider adapter 在首次外部请求前追加并确认 `ProviderStarted`。每个 effect 的 capability 只携 exact key，不能降格为 `SessionId`。

### 3. Settlement 与 failure policy

Host success evidence、logical cancel 与 session delete 提供确定外部 evidence；其他失败先交由 `execution-failure-policy` 产生 closed typed disposition command。`managed-chat-execution` 校验并单赋值追加 terminal fact。若 provider 尚未启动，terminal durable 后直接精确归还已绑定容量；若已经启动，则按 Host terminal evidence 与 typed policy 收敛，free-form diagnostic 不参与分支。

### 4. Activation 与 recovery

plugin construction 只组装 ports。durable store 激活后，recovery 折叠所有 nonterminal exact keys，并订阅 durable projection、capacity、Host evidence 与 typed failure 事件。每次事件重入同一 admission/settlement interpreter；等待仅存在于进程内，停止时丢弃，重启时从 durable facts 新建。cancel/delete 使用 projection 列出的 exact keys 驱动 settlement barrier，不设 timer 或 polling loop。

## DEPENDS ON

- `durable-events`
- `interaction-authority`
- `participant-identity`
- `execution-model-routing`
- `execution-failure-policy`
- `host-boundary`

## 计划验证与测试落点

| 命题 | 计划 executable proof |
|---|---|
| CHATEXEC-001 | `requirements/managed-chat-execution/tests/exact-execution-identity.test.mjs` |
| CHATEXEC-002 | `requirements/managed-chat-execution/tests/versioned-fact-replay.test.mjs` |
| CHATEXEC-003 | `requirements/managed-chat-execution/tests/admission-transaction-order.test.mjs` |
| CHATEXEC-004 | `requirements/managed-chat-execution/tests/accepted-idempotence.test.mjs` |
| CHATEXEC-005 | `requirements/managed-chat-execution/tests/provider-started-fence.test.mjs` |
| CHATEXEC-006 | `requirements/managed-chat-execution/tests/terminal-single-assignment.test.mjs` |
| CHATEXEC-007 | `requirements/managed-chat-execution/tests/pre-provider-settlement.test.mjs` |
| CHATEXEC-008 | `requirements/managed-chat-execution/tests/activation-recovery.test.mjs` |
| CHATEXEC-009 | `requirements/managed-chat-execution/tests/process-local-artifact-boundary.test.mjs` |
| CHATEXEC-010 | `requirements/managed-chat-execution/tests/cancel-delete-settlement.test.mjs` |
| CHATEXEC-011 | `requirements/managed-chat-execution/tests/attempt-execution-profile.test.mjs` |
